#include "sharpen.h"
#include "util.h"

int32_t sharpen(
    uint8_t* buffer,
    int32_t width,
    int32_t height,
    float amount
) {
    if (buffer == NULL) return 1;
    if (!isfinite(amount) || amount <= 0.0f) return 2;
    if (width <= 0 || height <= 0) return 3;

    size_t size = sizeof(RGBA) * width * height;

    RGBA* src = (RGBA*)malloc(size);
    if (src == NULL) return 4;
    memcpy(src, buffer, size);

    RGBA* _buffer = (RGBA*)buffer;
    for (size_t y = 1; y < height - 1; y++) {
        size_t row = y * width;

        for (size_t x = 1; x < width - 1; x++) {
            size_t i = row + x;

            RGBA c = src[i];
            RGBA l = src[i - 1];
            RGBA r = src[i + 1];
            RGBA u = src[i + width];
            RGBA d = src[i - width];

            float center = 1.0f + 4.0f * amount;
            float side = -amount;

            float rr = c.r * center + (l.r + r.r + u.r + d.r) * side;
            float gg = c.g * center + (l.g + r.g + u.g + d.g) * side;
            float bb = c.b * center + (l.b + r.b + u.b + d.b) * side;
            _buffer[i].r = round_to_byte(rr);
            _buffer[i].g = round_to_byte(gg);
            _buffer[i].b = round_to_byte(bb);
            _buffer[i].a = c.a;
        }
    }

    free(src);
    return 0;
}
