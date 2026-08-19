#pragma once
#include "common.h"

typedef enum BC7_RESULT {
    BC7_SUCCESS = 0,
    BC7_SOURCE_IS_NULL = 1,
    BC7_OUTPUT_IS_NULL = 2,
    BC7_WIDTH_IS_INVALID = 3,
    BC7_HEIGHT_IS_INVALID = 4,
    BC7_GENERATE_MIP_MAPS_IS_INVALID = 5,
    BC7_THREAD_COUNT_IS_INVALID = 6,
    BC7_SOURCE_SIZE_IS_INVALID = 7,
    BC7_OUTPUT_SIZE_IS_INVALID = 8,
    BC7_MIP_SIZE_IS_INVALID = 9,
    BC7_MIP_ALLOCATION_FAILED = 10,
} BC7_RESULT;

#ifdef __cplusplus
extern "C" {
#endif

// Source is one RGBA32 base level. Output is a contiguous BC7 mip chain.
// Each mip uses ceil(width / 4) * ceil(height / 4) * 16 bytes.
// Partial 4x4 blocks replicate their nearest edge pixel.
// Every block is encoded with BC7 mode 5.
// Thread count is clamped to the number of blocks and 64 internally.
__declspec(dllexport)
int32_t __cdecl bc7(
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
