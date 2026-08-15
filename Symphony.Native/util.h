#pragma once

#pragma pack(push, 1)
typedef struct RGBA {
    uint8_t r;
    uint8_t g;
    uint8_t b;
    uint8_t a;
} RGBA;
#pragma pack(pop)

__inline uint8_t round_to_byte(float value) {
	int r = (int)roundf(value);
	return r < 0 ? 0 : r > 255 ? 255 : r;
}

__inline int ceil_to_int(float value) {
	return (int)ceilf(value);
}

__inline int floor_to_int(float value) {
	return (int)floorf(value);
}

__inline int round_to_int(float value) {
	return (int)roundf(value);
}

__inline uint8_t float_to_byte_clamp(float value) {
	return (uint8_t)round_to_int(max(0.0f, min(1.0f, value)) * 255.0f);
}
