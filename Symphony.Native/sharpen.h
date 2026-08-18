#pragma once
#include "common.h"

#ifdef __cplusplus
extern "C" {
#endif

__declspec(dllexport)
int32_t sharpen(
    uint8_t* buffer,
    int32_t width,
    int32_t height,
    float amount
);

#ifdef __cplusplus
}
#endif
