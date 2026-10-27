// Native bits of the Unity host on iOS: haptic impacts (Native.impact → UIImpactFeedbackGenerator,
// as Capacitor's Haptics.impact did) and the share sheet (Share.cs → UIActivityViewController).
#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController(void);

static UIImpactFeedbackGenerator* sGen[3];

extern "C" void SisifosHaptics_Impact(int style)
{
    if (style < 0 || style > 2) style = 1;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (sGen[style] == nil)
        {
            UIImpactFeedbackStyle s = style == 0 ? UIImpactFeedbackStyleLight : style == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy;
            sGen[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        }
        [sGen[style] impactOccurred];
        [sGen[style] prepare];
    });
}

extern "C" void SisifosShare_Share(const char* text, const char* imagePath)
{
    NSString* t = text != NULL ? [NSString stringWithUTF8String:text] : @"";
    NSString* p = imagePath != NULL ? [NSString stringWithUTF8String:imagePath] : @"";
    dispatch_async(dispatch_get_main_queue(), ^{
        NSMutableArray* items = [NSMutableArray array];
        if (p.length > 0)
        {
            NSURL* url = [NSURL fileURLWithPath:p];
            if (url != nil) [items addObject:url];
        }
        if (t.length > 0) [items addObject:t];
        if (items.count == 0) return;
        UIViewController* root = UnityGetGLViewController();
        UIActivityViewController* vc = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        // iPad: anchor the popover at the middle of the screen
        if (vc.popoverPresentationController != nil)
        {
            vc.popoverPresentationController.sourceView = root.view;
            vc.popoverPresentationController.sourceRect = CGRectMake(CGRectGetMidX(root.view.bounds), CGRectGetMidY(root.view.bounds), 1, 1);
            vc.popoverPresentationController.permittedArrowDirections = 0;
        }
        [root presentViewController:vc animated:YES completion:nil];
    });
}
