#pragma once
#include "common.h"

typedef enum BC3_RESULT {
    BC3_SUCCESS = 0,
    BC3_SOURCE_IS_NULL = 1,
    BC3_OUTPUT_IS_NULL = 2,
    BC3_WIDTH_IS_INVALID = 3,
    BC3_HEIGHT_IS_INVALID = 4,
    BC3_GENERATE_MIP_MAPS_IS_INVALID = 5,
    BC3_THREAD_COUNT_IS_INVALID = 6,
    BC3_SOURCE_SIZE_IS_INVALID = 7,
    BC3_OUTPUT_SIZE_IS_INVALID = 8,
    BC3_MIP_SIZE_IS_INVALID = 9,
    BC3_MIP_ALLOCATION_FAILED = 10,
} BC3_RESULT;

#ifdef __cplusplus
extern "C" {
#endif

// Source is one RGBA32 base level. Output is a contiguous BC3/DXT5 mip chain.
// Each mip uses ceil(width / 4) * ceil(height / 4) * 16 bytes.
// Partial 4x4 blocks replicate their nearest edge pixel.
// Thread count is clamped to the number of blocks and 64 internally.
__declspec(dllexport)
int32_t __cdecl bc3(
    const uint8_t* source,
    size_t source_size,
    int32_t width,
    int32_t height,

    uint8_t* output,
    size_t output_size,

    uint8_t generate_mip_maps,
    int32_t thread_count
);

#ifdef __cplusplus
}
#endif
