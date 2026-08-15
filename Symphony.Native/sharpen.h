#pragma once
#include "common.h"

__declspec(dllexport)
int32_t sharpen(
    uint8_t* buffer,
    int32_t width,
    int32_t height,
    float amount
);
