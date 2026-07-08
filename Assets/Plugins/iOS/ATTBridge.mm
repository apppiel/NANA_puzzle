#import <AppTrackingTransparency/AppTrackingTransparency.h>

typedef void (*ATTCallback)(int status);

extern "C" {
    void _RequestATTPermission(ATTCallback callback) {
        if (@available(iOS 14, *)) {
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    if (callback) callback((int)status);
                });
            }];
        } else {
            // iOS 14 미만은 ATT 불필요, 바로 승인 처리
            if (callback) callback(3);
        }
    }
}
