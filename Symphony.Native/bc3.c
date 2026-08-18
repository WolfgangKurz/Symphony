#include "bc3.h"
#include "util.h"

#include <process.h>
#include <Windows.h>

#define STB_DXT_IMPLEMENTATION
#include "stb_dxt.h"

#define BC3_BLOCK_LENGTH 16
#define BC3_MAX_MIP_LEVELS 32
#define BC3_MAX_THREADS 64

typedef struct BC3_LEVEL {
    const uint8_t* pixels;
    int32_t width;
    int32_t height;
    size_t blocks_x;
    size_t block_offset;
    size_t block_count;
    uint8_t* output;
} BC3_LEVEL;

typedef struct BC3_WORKER {
    const BC3_LEVEL* levels;
    int32_t level_count;
    size_t first_block;
    size_t end_block;
} BC3_WORKER;

static int32_t checked_mul_size(size_t left, size_t right, size_t* result) {
    if (left != 0 && right > SIZE_MAX / left) return 0;

    *result = left * right;
    return 1;
}

static int32_t checked_add_size(size_t left, size_t right, size_t* result) {
    if (right > SIZE_MAX - left) return 0;

    *result = left + right;
    return 1;
}

static int32_t rgba_size(int32_t width, int32_t height, size_t* result) {
    size_t pixel_count;
    if (!checked_mul_size((size_t)width, (size_t)height, &pixel_count)) return 0;
    return checked_mul_size(pixel_count, sizeof(RGBA), result);
}

static int32_t initialize_levels(
    BC3_LEVEL* levels,
    int32_t width,
    int32_t height,
    uint8_t generate_mip_maps,
    size_t* output_size,
    size_t* block_count
) {
    int32_t level_count = 0;
    size_t total_output_size = 0;
    size_t total_block_count = 0;

    do {
        BC3_LEVEL* level = &levels[level_count];
        size_t level_output_size;

        level->pixels = NULL;
        level->width = width;
        level->height = height;
        level->blocks_x = ((size_t)width + 3) / 4;
        level->block_offset = total_block_count;

        size_t blocks_y = ((size_t)height + 3) / 4;
        if (!checked_mul_size(level->blocks_x, blocks_y, &level->block_count)) return 0;
        if (!checked_mul_size(level->block_count, BC3_BLOCK_LENGTH, &level_output_size)) return 0;
        if (!checked_add_size(total_output_size, level_output_size, &total_output_size)) return 0;
        if (!checked_add_size(total_block_count, level->block_count, &total_block_count)) return 0;

        level_count++;
        if (generate_mip_maps == 0 || (width == 1 && height == 1)) break;

        width = width > 1 ? width / 2 : 1;
        height = height > 1 ? height / 2 : 1;
    } while (level_count < BC3_MAX_MIP_LEVELS);

    *output_size = total_output_size;
    *block_count = total_block_count;
    return level_count;
}

static void generate_mip(const BC3_LEVEL* source, BC3_LEVEL* output) {
    const RGBA* source_pixels = (const RGBA*)source->pixels;
    RGBA* output_pixels = (RGBA*)output->pixels;

    for (int32_t y = 0; y < output->height; y++) {
        int32_t source_y0 = y * 2;
        int32_t source_y1 = source_y0 + 1;
        if (source_y1 >= source->height) source_y1 = source->height - 1;

        for (int32_t x = 0; x < output->width; x++) {
            int32_t source_x0 = x * 2;
            int32_t source_x1 = source_x0 + 1;
            if (source_x1 >= source->width) source_x1 = source->width - 1;

            RGBA c00 = source_pixels[(size_t)source_y0 * source->width + source_x0];
            RGBA c10 = source_pixels[(size_t)source_y0 * source->width + source_x1];
            RGBA c01 = source_pixels[(size_t)source_y1 * source->width + source_x0];
            RGBA c11 = source_pixels[(size_t)source_y1 * source->width + source_x1];

            RGBA* pixel = &output_pixels[(size_t)y * output->width + x];
            pixel->r = (uint8_t)(((uint32_t)c00.r + c10.r + c01.r + c11.r) / 4);
            pixel->g = (uint8_t)(((uint32_t)c00.g + c10.g + c01.g + c11.g) / 4);
            pixel->b = (uint8_t)(((uint32_t)c00.b + c10.b + c01.b + c11.b) / 4);
            pixel->a = (uint8_t)(((uint32_t)c00.a + c10.a + c01.a + c11.a) / 4);
        }
    }
}

static void copy_block(const BC3_LEVEL* level, size_t block_index, uint8_t* block) {
    size_t block_x = block_index % level->blocks_x;
    size_t block_y = block_index / level->blocks_x;

    for (size_t y = 0; y < 4; y++) {
        size_t source_y = block_y * 4 + y;
        if (source_y >= (size_t)level->height) source_y = (size_t)level->height - 1;

        for (size_t x = 0; x < 4; x++) {
            size_t source_x = block_x * 4 + x;
            if (source_x >= (size_t)level->width) source_x = (size_t)level->width - 1;

            const uint8_t* source = level->pixels + (source_y * level->width + source_x) * sizeof(RGBA);
            memcpy(block + (y * 4 + x) * sizeof(RGBA), source, sizeof(RGBA));
        }
    }
}

static void compress_range(const BC3_WORKER* worker) {
    uint8_t block[4 * 4 * sizeof(RGBA)];

    for (int32_t level_index = 0; level_index < worker->level_count; level_index++) {
        const BC3_LEVEL* level = &worker->levels[level_index];
        size_t level_end = level->block_offset + level->block_count;
        if (worker->first_block >= level_end) continue;
        if (worker->end_block <= level->block_offset) break;

        size_t first = worker->first_block > level->block_offset
            ? worker->first_block - level->block_offset
            : 0;
        size_t end = worker->end_block < level_end
            ? worker->end_block - level->block_offset
            : level->block_count;

        for (size_t block_index = first; block_index < end; block_index++) {
            copy_block(level, block_index, block);
            stb_compress_dxt_block(
                level->output + block_index * BC3_BLOCK_LENGTH,
                block,
                1,
                STB_DXT_NORMAL
            );
        }
    }
}

static unsigned int __stdcall compress_worker(void* state) {
    compress_range((const BC3_WORKER*)state);
    return 0;
}

static void compress_levels(
    const BC3_LEVEL* levels,
    int32_t level_count,
    size_t block_count,
    int32_t requested_thread_count
) {
    int32_t thread_count = requested_thread_count;
    if (thread_count > BC3_MAX_THREADS) thread_count = BC3_MAX_THREADS;
    if ((size_t)thread_count > block_count) thread_count = (int32_t)block_count;

    BC3_WORKER workers[BC3_MAX_THREADS];
    HANDLE handles[BC3_MAX_THREADS - 1];
    int32_t handle_count = 0;
    size_t blocks_per_thread = block_count / (size_t)thread_count;
    size_t remaining_blocks = block_count % (size_t)thread_count;

    for (int32_t i = 0; i < thread_count; i++) {
        size_t extra_before = (size_t)i < remaining_blocks ? (size_t)i : remaining_blocks;
        size_t blocks_for_thread = blocks_per_thread + ((size_t)i < remaining_blocks ? 1 : 0);

        workers[i].levels = levels;
        workers[i].level_count = level_count;
        workers[i].first_block = blocks_per_thread * (size_t)i + extra_before;
        workers[i].end_block = workers[i].first_block + blocks_for_thread;
    }

    for (int32_t i = 1; i < thread_count; i++) {
        uintptr_t handle = _beginthreadex(NULL, 0, compress_worker, &workers[i], 0, NULL);
        if (handle == 0) {
            compress_range(&workers[i]);
            continue;
        }

        handles[handle_count++] = (HANDLE)handle;
    }

    compress_range(&workers[0]);

    for (int32_t i = 0; i < handle_count; i++) {
        WaitForSingleObject(handles[i], INFINITE);
        CloseHandle(handles[i]);
    }
}

int32_t __cdecl bc3(
    const uint8_t* source,
    size_t source_size,
    int32_t width,
    int32_t height,

    uint8_t* output,
    size_t output_size,

    uint8_t generate_mip_maps,
    int32_t thread_count
) {
    if (source == NULL) return BC3_SOURCE_IS_NULL;
    if (output == NULL) return BC3_OUTPUT_IS_NULL;
    if (width <= 0) return BC3_WIDTH_IS_INVALID;
    if (height <= 0) return BC3_HEIGHT_IS_INVALID;
    if (generate_mip_maps > 1) return BC3_GENERATE_MIP_MAPS_IS_INVALID;
    if (thread_count <= 0) return BC3_THREAD_COUNT_IS_INVALID;

    size_t expected_source_size;
    if (!rgba_size(width, height, &expected_source_size) || source_size != expected_source_size)
        return BC3_SOURCE_SIZE_IS_INVALID;

    BC3_LEVEL levels[BC3_MAX_MIP_LEVELS];
    size_t expected_output_size;
    size_t block_count;
    int32_t level_count = initialize_levels(
        levels,
        width,
        height,
        generate_mip_maps,
        &expected_output_size,
        &block_count
    );
    if (level_count == 0 || output_size != expected_output_size) return BC3_OUTPUT_SIZE_IS_INVALID;

    levels[0].pixels = source;
    size_t output_offset = 0;
    for (int32_t i = 0; i < level_count; i++) {
        levels[i].output = output + output_offset;
        output_offset += levels[i].block_count * BC3_BLOCK_LENGTH;

        if (i == 0) continue;

        size_t level_size;
        if (!rgba_size(levels[i].width, levels[i].height, &level_size)) {
            for (int32_t j = 1; j < i; j++) free((void*)levels[j].pixels);
            return BC3_MIP_SIZE_IS_INVALID;
        }

        levels[i].pixels = (const uint8_t*)malloc(level_size);
        if (levels[i].pixels == NULL) {
            for (int32_t j = 1; j < i; j++) free((void*)levels[j].pixels);
            return BC3_MIP_ALLOCATION_FAILED;
        }

        generate_mip(&levels[i - 1], &levels[i]);
    }

    compress_levels(levels, level_count, block_count, thread_count);

    for (int32_t i = 1; i < level_count; i++) free((void*)levels[i].pixels);
    return BC3_SUCCESS;
}
