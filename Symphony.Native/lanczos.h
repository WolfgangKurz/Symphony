#pragma once
#include "common.h"

#define LANCZOS_RADIUS (2.0f)

__declspec(dllexport)
int32_t lanczos2(
    const uint8_t* source,
    size_t source_size,
    int32_t src_width,
    int32_t src_height,

    uint8_t* output,
    size_t output_size,
    int32_t out_width,
    int32_t out_height,

    uint8_t pma,
    float filter_tightness
);
