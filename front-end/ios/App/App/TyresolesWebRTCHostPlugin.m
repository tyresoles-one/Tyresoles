#import <Foundation/Foundation.h>
#import <Capacitor/Capacitor.h>

CAP_PLUGIN(TyresolesWebRTCHostPlugin, "TyresolesWebRTCHost",
    CAP_PLUGIN_METHOD(initializeSession, CAPPluginReturnPromise);
    CAP_PLUGIN_METHOD(startBroadcast, CAPPluginReturnPromise);
    CAP_PLUGIN_METHOD(stopBroadcast, CAPPluginReturnPromise);
)
