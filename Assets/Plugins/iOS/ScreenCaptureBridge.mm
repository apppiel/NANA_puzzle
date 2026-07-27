#import <UIKit/UIKit.h>

extern "C" {
    bool _IsScreenBeingCaptured() {
        return [UIScreen mainScreen].isCaptured;
    }
}
