// CoreText text rasteriser for Sisifos.App.IosText (Assets/Scripts/App/TextEngine.cs).
// A run is laid out with CTLine (shaping, bidi, cascade-list font fallback for scripts the
// font lacks) and drawn white into an alpha-only bitmap, top row first.
#import <Foundation/Foundation.h>
#import <CoreText/CoreText.h>
#import <CoreGraphics/CoreGraphics.h>
#include <math.h>
#include <stdlib.h>
#include <string.h>

static NSMutableDictionary<NSString*, id>* sFonts;

static NSString* MakeString(const unsigned char* utf8, int len)
{
    if (utf8 == NULL || len <= 0) return @"";
    NSString* s = [[NSString alloc] initWithBytes:utf8 length:(NSUInteger)len encoding:NSUTF8StringEncoding];
    return s != nil ? s : @"";
}

static CGFloat CTWeight(int css)
{
    // CSS weight → kCTFontWeightTrait (UIFontWeight values)
    if (css <= 100) return -0.8;
    if (css <= 200) return -0.6;
    if (css <= 300) return -0.4;
    if (css <= 400) return 0.0;
    if (css <= 500) return 0.23;
    if (css <= 600) return 0.3;
    if (css <= 700) return 0.4;
    if (css <= 800) return 0.56;
    return 0.62;
}

// A CTFont: from a bundled file (CGFontRef) or by family name and traits. Cached.
static CTFontRef GetFont(void* cgFont, const unsigned char* family, int familyLen, int weight, int italic, float size)
{
    if (sFonts == nil) sFonts = [NSMutableDictionary dictionary];
    NSString* fam = MakeString(family, familyLen);
    NSString* key = [NSString stringWithFormat:@"%p|%@|%d|%d|%.3f", cgFont, fam, weight, italic, size];
    id cached = sFonts[key];
    if (cached != nil) return (__bridge CTFontRef)cached;
    CTFontRef font = NULL;
    if (cgFont != NULL)
    {
        font = CTFontCreateWithGraphicsFont((CGFontRef)cgFont, size, NULL, NULL);
    }
    else
    {
        if (fam.length == 0) fam = @"Times New Roman";
        NSMutableDictionary* traits = [NSMutableDictionary dictionary];
        traits[(__bridge id)kCTFontWeightTrait] = @(CTWeight(weight));
        CTFontSymbolicTraits sym = 0;
        if (italic) sym |= kCTFontItalicTrait;
        if (weight >= 600) sym |= kCTFontBoldTrait;
        traits[(__bridge id)kCTFontSymbolicTrait] = @(sym);
        NSDictionary* attrs = @{
            (__bridge id)kCTFontFamilyNameAttribute: fam,
            (__bridge id)kCTFontTraitsAttribute: traits,
        };
        CTFontDescriptorRef desc = CTFontDescriptorCreateWithAttributes((__bridge CFDictionaryRef)attrs);
        font = CTFontCreateWithFontDescriptor(desc, size, NULL);
        CFRelease(desc);
    }
    if (font == NULL) font = CTFontCreateWithName(CFSTR("TimesNewRomanPSMT"), size, NULL);
    if (sFonts.count > 256) [sFonts removeAllObjects];
    sFonts[key] = (__bridge_transfer id)font;
    return font;
}

static CTLineRef MakeLine(NSString* text, CTFontRef font)
{
    NSDictionary* attrs = @{
        (__bridge id)kCTFontAttributeName: (__bridge id)font,
        (__bridge id)kCTForegroundColorFromContextAttributeName: @YES,
    };
    NSAttributedString* as = [[NSAttributedString alloc] initWithString:text attributes:attrs];
    return CTLineCreateWithAttributedString((__bridge CFAttributedStringRef)as);
}

extern "C" {

void* SisifosText_LoadFont(const unsigned char* utf8Path, int len)
{
    NSString* path = MakeString(utf8Path, len);
    CGDataProviderRef provider = CGDataProviderCreateWithFilename([path fileSystemRepresentation]);
    if (provider == NULL) return NULL;
    CGFontRef font = CGFontCreateWithDataProvider(provider);
    CGDataProviderRelease(provider);
    return (void*)font; // kept for the life of the app
}

float SisifosText_Measure(const unsigned char* utf8, int len, void* cgFont, const unsigned char* family, int familyLen,
    int weight, int italic, float size)
{
    @autoreleasepool
    {
        CTFontRef font = GetFont(cgFont, family, familyLen, weight, italic, size);
        CTLineRef line = MakeLine(MakeString(utf8, len), font);
        double w = CTLineGetTypographicBounds(line, NULL, NULL, NULL);
        CFRelease(line);
        return (float)w;
    }
}

void SisifosText_Metrics(void* cgFont, const unsigned char* family, int familyLen, int weight, int italic, float size,
    float* ascent, float* descent)
{
    @autoreleasepool
    {
        CTFontRef font = GetFont(cgFont, family, familyLen, weight, italic, size);
        *ascent = (float)CTFontGetAscent(font);
        *descent = (float)CTFontGetDescent(font);
    }
}

unsigned char* SisifosText_Raster(const unsigned char* utf8, int len, void* cgFont, const unsigned char* family, int familyLen,
    int weight, int italic, float size, float strokeWidth, int* outW, int* outH, int* outStride, float* originX, float* originY)
{
    @autoreleasepool
    {
        CTFontRef font = GetFont(cgFont, family, familyLen, weight, italic, size);
        CTLineRef line = MakeLine(MakeString(utf8, len), font);
        CGFloat asc = 0, desc = 0, lead = 0;
        double adv = CTLineGetTypographicBounds(line, &asc, &desc, &lead);
        // room for glyphs that overhang their advance or the line's ascent (accents, italics)
        int pad = (int)ceil(size * 0.3f + (strokeWidth > 0 ? strokeWidth : 0));
        int w = (int)ceil(adv) + 2 * pad;
        int h = (int)ceil(asc + desc) + 2 * pad;
        if (w <= 0 || h <= 0) { CFRelease(line); return NULL; }
        int stride = (w + 3) & ~3;
        unsigned char* data = (unsigned char*)calloc((size_t)stride * (size_t)h, 1);
        if (data == NULL) { CFRelease(line); return NULL; }
        CGContextRef ctx = CGBitmapContextCreate(data, (size_t)w, (size_t)h, 8, (size_t)stride, NULL, (CGBitmapInfo)kCGImageAlphaOnly);
        if (ctx == NULL) { free(data); CFRelease(line); return NULL; }
        CGContextSetAllowsAntialiasing(ctx, true);
        CGContextSetShouldAntialias(ctx, true);
        CGContextSetAllowsFontSmoothing(ctx, false);
        CGContextSetShouldSmoothFonts(ctx, false);
        CGContextSetGrayFillColor(ctx, 1.0, 1.0);
        CGContextSetGrayStrokeColor(ctx, 1.0, 1.0);
        if (strokeWidth > 0)
        {
            CGContextSetTextDrawingMode(ctx, kCGTextStroke);
            CGContextSetLineWidth(ctx, strokeWidth);
            CGContextSetLineJoin(ctx, kCGLineJoinMiter);
            CGContextSetMiterLimit(ctx, 10);
        }
        else CGContextSetTextDrawingMode(ctx, kCGTextFill);
        // CoreGraphics is y up from the bottom; the first row of `data` is the top of the image
        float baselineFromTop = pad + (float)asc;
        CGContextSetTextMatrix(ctx, CGAffineTransformIdentity);
        CGContextSetTextPosition(ctx, pad, h - baselineFromTop);
        CTLineDraw(line, ctx);
        CGContextRelease(ctx);
        CFRelease(line);
        *outW = w; *outH = h; *outStride = stride;
        *originX = (float)pad;
        *originY = baselineFromTop;
        return data;
    }
}

void SisifosText_Free(void* p)
{
    free(p);
}

}
