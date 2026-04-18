package com.tyresoles.app;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import androidx.activity.result.ActivityResult;
import androidx.activity.result.ActivityResultCallback;
import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;

import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

import org.webrtc.DefaultVideoDecoderFactory;
import org.webrtc.DefaultVideoEncoderFactory;
import org.webrtc.EglBase;
import org.webrtc.PeerConnection;
import org.webrtc.PeerConnectionFactory;
import org.webrtc.ScreenCapturerAndroid;
import org.webrtc.SurfaceTextureHelper;
import org.webrtc.VideoSource;
import org.webrtc.VideoTrack;

@CapacitorPlugin(name = "TyresolesWebRTCHost")
public class TyresolesWebRTCHostPlugin extends Plugin {

    private PeerConnectionFactory peerConnectionFactory;
    private PeerConnection peerConnection;
    private VideoTrack localVideoTrack;
    private VideoSource videoSource;
    private SurfaceTextureHelper surfaceTextureHelper;
    private ScreenCapturerAndroid screenCapturer;
    
    private MediaProjectionManager mediaProjectionManager;
    private EglBase rootEglBase;
    
    private String savedToken;
    private String savedSessionId;

    @PluginMethod
    public void initializeSession(PluginCall call) {
        savedToken = call.getString("token");
        savedSessionId = call.getString("sessionId");
        
        // Initialize WebRTC globals
        Context context = getContext();
        PeerConnectionFactory.InitializationOptions initializationOptions =
                PeerConnectionFactory.InitializationOptions.builder(context)
                        .setEnableInternalTracer(true)
                        .createInitializationOptions();
        PeerConnectionFactory.initialize(initializationOptions);

        rootEglBase = EglBase.create();
        PeerConnectionFactory.Options options = new PeerConnectionFactory.Options();
        DefaultVideoEncoderFactory defaultVideoEncoderFactory = new DefaultVideoEncoderFactory(
                rootEglBase.getEglBaseContext(), true, true);
        DefaultVideoDecoderFactory defaultVideoDecoderFactory = new DefaultVideoDecoderFactory(
                rootEglBase.getEglBaseContext());

        peerConnectionFactory = PeerConnectionFactory.builder()
                .setOptions(options)
                .setVideoEncoderFactory(defaultVideoEncoderFactory)
                .setVideoDecoderFactory(defaultVideoDecoderFactory)
                .createPeerConnectionFactory();
        
        JSObject ret = new JSObject();
        ret.put("success", true);
        call.resolve(ret);
    }

    @PluginMethod
    public void startBroadcast(final PluginCall call) {
        mediaProjectionManager = (MediaProjectionManager) getContext().getSystemService(Context.MEDIA_PROJECTION_SERVICE);
        if (mediaProjectionManager == null) {
            call.reject("MediaProjectionManager is null");
            return;
        }

        Intent captureIntent = mediaProjectionManager.createScreenCaptureIntent();

        // Using Capacitor's integrated Activity Result launch system
        startActivityForResult(call, captureIntent, "mediaProjectionResult");
    }

    @Override
    protected void handleOnActivityResult(int requestCode, int resultCode, Intent data) {
        super.handleOnActivityResult(requestCode, resultCode, data);

        // Find the original saved call
        PluginCall call = getSavedCall();
        if (call == null) return;

        if (resultCode != Activity.RESULT_OK) {
            call.reject("User declined screen recording access");
            return;
        }

        startCapturing(data);
        
        JSObject ret = new JSObject();
        ret.put("success", true);
        call.resolve(ret);
        
        notifyListeners("onConnectionStateChange", new JSObject().put("state", "connected"));
    }

    private void startCapturing(Intent mediaProjectionData) {
        screenCapturer = new ScreenCapturerAndroid(mediaProjectionData, new MediaProjection.Callback() {
            @Override
            public void onStop() {
                notifyListeners("onBroadcastStopped", new JSObject());
            }
        });

        surfaceTextureHelper = SurfaceTextureHelper.create("CaptureThread", rootEglBase.getEglBaseContext());
        videoSource = peerConnectionFactory.createVideoSource(screenCapturer.isScreencast());
        screenCapturer.initialize(surfaceTextureHelper, getContext(), videoSource.getCapturerObserver());
        
        // Start capture at standard resolving 720p @ 30fps
        screenCapturer.startCapture(1280, 720, 30);
        
        localVideoTrack = peerConnectionFactory.createVideoTrack("100", videoSource);
        
        // Create PeerConnection configured for the Tyresoles hub
        PeerConnection.RTCConfiguration rtcConfig = new PeerConnection.RTCConfiguration(new java.util.ArrayList<>());
        /* 
         * TODO: Here native team matches the TS `RemoteAssistSignalingRoom` logic:
         * 1. Initialize custom WebSocket listener to ws://hub.
         * 2. pc = peerConnectionFactory.createPeerConnection(rtcConfig, observer).
         * 3. pc.addTrack(localVideoTrack).
         * 4. pc.createOffer() and send SDP back to C# server. 
         */
    }

    @PluginMethod
    public void stopBroadcast(PluginCall call) {
        if (screenCapturer != null) {
            try {
                screenCapturer.stopCapture();
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
            screenCapturer.dispose();
            screenCapturer = null;
        }
        if (videoSource != null) {
            videoSource.dispose();
            videoSource = null;
        }
        if (surfaceTextureHelper != null) {
            surfaceTextureHelper.dispose();
            surfaceTextureHelper = null;
        }
        if (localVideoTrack != null) {
            localVideoTrack.dispose();
            localVideoTrack = null;
        }
        if (peerConnection != null) {
            peerConnection.close();
            peerConnection = null;
        }

        notifyListeners("onBroadcastStopped", new JSObject());
        call.resolve();
    }
}
