#include "lanczos.h"
#include "util.h"

typedef struct AXIS_WEIGHTS {
    int32_t size;

    int32_t* starts;
    int32_t* counts;
    int32_t* offsets;
    float* values;
} AXIS_WEIGHTS;

typedef struct RGBAf {
    float r;
    float g;
    float b;
    float a;
} RGBAf;

float sinc(float x) {
    if (fabsf(x) < 1e-6f) return 1.0f;

    double px = 3.14159265358979323846264 * x;
    return (float)(sin(px) / px);
}
float lanczos2_distance(float x) {
    float y = fabsf(x);
    if (y < 1e-6f) return 1.0f;
    if (y >= LANCZOS_RADIUS) return 0.0f;
    return sinc(x) * sinc(x * 0.5f);
}
AXIS_WEIGHTS axis_weights(int32_t src_size, int32_t dst_size, float filter_tightness) {
    AXIS_WEIGHTS ret;
    memset(&ret, 0, sizeof(AXIS_WEIGHTS));

    float scale = (float)dst_size / src_size;
    float filter_scale = min(1.0f, scale * filter_tightness);
    float support = LANCZOS_RADIUS / filter_scale;

    int32_t* starts = (int32_t*)malloc(sizeof(int32_t) * dst_size);
    if (starts == NULL) return ret;

    int32_t* counts = (int32_t*)malloc(sizeof(int32_t) * dst_size);
    if (counts == NULL) {
        free(starts);
        return ret;
    }

    int32_t* offsets = (int32_t*)malloc(sizeof(int32_t) * dst_size);
    if (offsets == NULL) {
        free(counts);
        free(starts);
        return ret;
    }

    // count weights
    int32_t total_weights = 0;
    for (int d = 0; d < dst_size; d++) {
        float center = (0.5f + d) / scale - 0.5f;

        int left = max(0, ceil_to_int(center - support));
        int right = min(src_size - 1, floor_to_int(center + support));
        int count = right - left + 1;

        starts[d] = left;
        counts[d] = count;
        offsets[d] = total_weights;

        total_weights += count;
    }

    // calc weights
    float* values = (float*)malloc(sizeof(float) * total_weights);
    if (values == NULL) {
        free(offsets);
        free(counts);
        free(starts);
        return ret;
    }

    for (int d = 0; d < dst_size; d++) {
        float center = (0.5f + d) / scale - 0.5f;

        int start = starts[d];
        int count = counts[d];
        int offset = offsets[d];

        float sum = 0.0f;
        for (int32_t i = 0; i < count; i++) {
            int src = start + i;
            float distance = ((float)src - center) * filter_scale;
            float w = lanczos2_distance(distance);

            values[offset + i] = w;
            sum += w;
        }

        // normalize (DC gain = 1)
        if (fabsf(sum) > 1e-8f) {
            float inv = 1.0f / sum;
            for (int32_t i = 0; i < count; i++)
                values[offset + i] *= inv;
        }
        else { // for safety
            for (int32_t i = 0; i < count; i++)
                values[offset + i] = 0.0f;

            // clamp
            int nearest = max(0, min(count - 1, round_to_int(center) - start));
            values[offset + nearest] = 1.0f;
        }
    }

    ret.size = dst_size;
    ret.starts = starts;
    ret.counts = counts;
    ret.offsets = offsets;
    ret.values = values;
    return ret;
}
void free_axis_weights(AXIS_WEIGHTS* weights) {
    if (weights == NULL) return;

    if (weights->starts != NULL) {
        free(weights->starts);
        weights->starts = NULL;
    }
    if (weights->counts != NULL) {
        free(weights->counts);
        weights->counts = NULL;
    }
    if (weights->offsets != NULL) {
        free(weights->offsets);
        weights->offsets = NULL;
    }
    if (weights->values != NULL) {
        free(weights->values);
        weights->values = NULL;
    }
}

void horz_pass(const uint8_t* source, int32_t src_width, int32_t src_height, RGBAf* dest, int32_t dst_width, AXIS_WEIGHTS weights, uint8_t pma) {
    const float inv255 = 1.0f / 255.0f;

    const RGBA* src = (const RGBA*)source;

    for (int32_t y = 0; y < src_height; y++) {
        int32_t src_row = y * src_width;
        int32_t dst_row = y * dst_width;

        for (int32_t dx = 0; dx < dst_width; dx++) {
            int32_t src_start = weights.starts[dx];
            int32_t count = weights.counts[dx];
            int32_t offset = weights.offsets[dx];

            float r = 0.0f, g = 0.0f, b = 0.0f, a = 0.0f;
            for (int32_t i = 0; i < count; i++) {
                RGBA c = src[src_row + src_start + i];
                float w = weights.values[offset + i];

                float alpha = inv255 * c.a;
                float cr = inv255 * c.r;
                float cg = inv255 * c.g;
                float cb = inv255 * c.b;

                if (pma != 0) {
                    cr *= alpha;
                    cg *= alpha;
                    cb *= alpha;
                }

                r += cr * w;
                g += cg * w;
                b += cb * w;
                a += alpha * w;
            }

            dest[dst_row + dx].r = r;
            dest[dst_row + dx].g = g;
            dest[dst_row + dx].b = b;
            dest[dst_row + dx].a = a;
        }
    }
}
void vert_pass(RGBAf* src, int32_t width, int32_t src_height, uint8_t* dest, int32_t dst_height, AXIS_WEIGHTS weights, uint8_t pma) {
    for (int32_t dy = 0; dy < dst_height; dy++) {
        int32_t src_start = weights.starts[dy];
        int32_t count = weights.counts[dy];
        int32_t offset = weights.offsets[dy];

        int32_t dst_row = dy * width;

        RGBA* dst = (RGBA*)dest;

        for (int32_t x = 0; x < width; x++) {
            float r = 0.0f, g = 0.0f, b = 0.0f, a = 0.0f;

            for (int32_t i = 0; i < count; i++) {
                RGBAf c = src[(src_start + i) * width + x];
                float w = weights.values[offset + i];

                r += c.r * w;
                g += c.g * w;
                b += c.b * w;
                a += c.a * w;
            }

            if (pma != 0) {
                if (a > 1e-6f) {
                    r /= a;
                    g /= a;
                    b /= a;
                }
                else
                    r = g = b = 0.0f;
            }

            dst[dst_row + x].r = float_to_byte_clamp(r);
            dst[dst_row + x].g = float_to_byte_clamp(g);
            dst[dst_row + x].b = float_to_byte_clamp(b);
            dst[dst_row + x].a = float_to_byte_clamp(a);
        }
    }
}

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
) {
    if (source == NULL) return 1;
    if (output == NULL) return 2;

    if (src_width <= 0) return 3;
    if (src_height <= 0) return 4;
    if (out_width <= 0) return 5;
    if (out_height <= 0) return 6;

    if (!isfinite(filter_tightness) || filter_tightness <= 0.0f) return 7;

    if (source_size != (size_t)4 * src_width * src_height) return 8;
    if (output_size != (size_t)4 * out_width * out_height) return 9;

    if (src_width == out_width && src_height == out_height) {
        memcpy(output, source, output_size);
        return 0;
    }

    // horizontal pass
    RGBAf* horz = (RGBAf*)malloc(sizeof(RGBAf) * out_width * src_height);
    if (horz == NULL) return 10;

    // pre-calculate lanczos weights
    AXIS_WEIGHTS weights_x = axis_weights(src_width, out_width, filter_tightness);
    if (weights_x.size == 0) {
        free(horz);
        return 11;
    }

    AXIS_WEIGHTS weights_y = axis_weights(src_height, out_height, filter_tightness);
    if (weights_y.size == 0) {
        free_axis_weights(&weights_x);
        free(horz);
        return 12;
    }

    horz_pass(source, src_width, src_height, horz, out_width, weights_x, pma);
    vert_pass(horz, out_width, src_height, output, out_height, weights_y, pma);

    // cleanup
    free_axis_weights(&weights_y);
    free_axis_weights(&weights_x);
    free(horz);

    return 0;
}
