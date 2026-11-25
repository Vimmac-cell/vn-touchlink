package com.vnk.phonelink;

import android.app.Activity;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.view.HapticFeedbackConstants;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;

public class MainActivity extends Activity {

    private Link link;
    private TouchpadView pad;
    private ArtPadView art;
    private FrameLayout root;
    private TextView modeBtn, undoBtn, redoBtn, eraserBtn;
    private boolean artMode, eraser;
    private float dp;

    // ----- Clipboard sharing -----
    // Android only lets an app read the clipboard while it is on screen, so the phone sends
    // its copied text when you open this app (or copy while it is open).
    private final android.os.Handler handler = new android.os.Handler(android.os.Looper.getMainLooper());
    private android.content.ClipboardManager clipboardManager;
    private final android.content.ClipboardManager.OnPrimaryClipChangedListener clipListener =
            () -> handler.postDelayed(this::sendClipboardIfNew, 100);

    private String lastSynced() { return prefs().getString("clipSynced", null); }

    private void setLastSynced(String t) { prefs().edit().putString("clipSynced", t).apply(); }

    private void sendClipboardIfNew() {
        if (!link.isConnected() || clipboardManager == null || !hasWindowFocus()) {
            android.util.Log.i("VNTouchLink", "clip: skip connected=" + link.isConnected() + " focus=" + hasWindowFocus());
            return;
        }
        try {
            android.content.ClipData clip = clipboardManager.getPrimaryClip();
            if (clip == null || clip.getItemCount() == 0) return;
            CharSequence cs = clip.getItemAt(0).coerceToText(this);
            if (cs == null) return;
            String text = cs.toString();
            if (text.isEmpty() || text.equals(lastSynced())) { android.util.Log.i("VNTouchLink", "clip: same as last sync, not sent"); return; }
            byte[] bytes = text.getBytes(java.nio.charset.StandardCharsets.UTF_8);
            if (bytes.length > 1_000_000) return;
            link.send("CB " + android.util.Base64.encodeToString(bytes, android.util.Base64.NO_WRAP));
            android.util.Log.i("VNTouchLink", "clip: sent " + bytes.length + " bytes");
            setLastSynced(text);
        } catch (Exception e) { android.util.Log.w("VNTouchLink", "clip read failed", e); }
    }

    private void setFromPc(String text) {
        setLastSynced(text); // first, so our own change isn't sent back
        if (clipboardManager != null) {
            clipboardManager.setPrimaryClip(android.content.ClipData.newPlainText("VN TouchLink", text));
        }
    }

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        getWindow().getAttributes().layoutInDisplayCutoutMode =
                WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
        dp = getResources().getDisplayMetrics().density;
        clipboardManager = (android.content.ClipboardManager) getSystemService(CLIPBOARD_SERVICE);

        link = new Link(new Link.Listener() {
            @Override public void onState(boolean c) {
                runOnUiThread(() -> {
                    pad.setConnected(c);
                    applyVisibility(); // not connected: always show the "waiting / get the PC app" screen
                    if (c && artMode) link.send("PE " + (eraser ? "1" : "0"));
                    if (c) handler.postDelayed(MainActivity.this::sendClipboardIfNew, 300);
                });
            }
            @Override public void onScreen(int w, int h) { runOnUiThread(() -> art.setScreen(w, h)); }
            @Override public void onClipboard(String text) { runOnUiThread(() -> setFromPc(text)); }
        });
        pad = new TouchpadView(this, link);
        art = new ArtPadView(this, link);

        root = new FrameLayout(this);
        root.addView(pad, new FrameLayout.LayoutParams(-1, -1));
        LinearLayout artBox = new LinearLayout(this);
        artBox.setOrientation(LinearLayout.HORIZONTAL);
        artBox.addView(new View(this), new LinearLayout.LayoutParams((int) (56 * dp), -1)); // room for the toolbar
        artBox.addView(art, new LinearLayout.LayoutParams(0, -1, 1f));
        root.addView(artBox, new FrameLayout.LayoutParams(-1, -1));
        root.addView(buildToolbar(), new FrameLayout.LayoutParams((int) (56 * dp), -1, Gravity.START));
        setContentView(root);

        link.setToken(prefs().getString("token", ""));
        readToken(getIntent());
        setArtMode(prefs().getBoolean("artMode", false));
    }

    private View buildToolbar() {
        LinearLayout bar = new LinearLayout(this);
        bar.setOrientation(LinearLayout.VERTICAL);
        bar.setGravity(Gravity.CENTER_HORIZONTAL);
        int pad = (int) (6 * dp);
        bar.setPadding(pad, pad, pad, pad);
        modeBtn = button("Art", v -> setArtMode(!artMode));
        undoBtn = button("Undo", v -> link.send("G art_undo"));
        redoBtn = button("Redo", v -> link.send("G art_redo"));
        eraserBtn = button("Erase", v -> setEraser(!eraser));
        for (TextView b : new TextView[]{modeBtn, undoBtn, redoBtn, eraserBtn}) {
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(-1, (int) (44 * dp));
            lp.bottomMargin = (int) (8 * dp);
            bar.addView(b, lp);
        }
        return bar;
    }

    private TextView button(String text, View.OnClickListener click) {
        TextView b = new TextView(this);
        b.setText(text);
        b.setTextSize(12);
        b.setTextColor(Color.rgb(200, 200, 210));
        b.setGravity(Gravity.CENTER);
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(Color.rgb(32, 32, 40));
        bg.setCornerRadius(10 * dp);
        b.setBackground(bg);
        b.setOnClickListener(v -> { v.performHapticFeedback(HapticFeedbackConstants.KEYBOARD_TAP); click.onClick(v); });
        return b;
    }

    private void setActive(TextView b, boolean on) {
        ((GradientDrawable) b.getBackground()).setColor(on ? Color.rgb(50, 90, 180) : Color.rgb(32, 32, 40));
    }

    private void setArtMode(boolean on) {
        if (artMode && !on) art.release();
        artMode = on;
        prefs().edit().putBoolean("artMode", on).apply();
        applyVisibility();
        if (on) link.send("PE " + (eraser ? "1" : "0"));
    }

    private void applyVisibility() {
        boolean connected = link.isConnected();
        boolean showArt = artMode && connected;
        ((View) art.getParent()).setVisibility(showArt ? View.VISIBLE : View.GONE);
        pad.setVisibility(showArt ? View.GONE : View.VISIBLE);
        modeBtn.setVisibility(connected ? View.VISIBLE : View.GONE);
        modeBtn.setText(artMode ? "Mouse" : "Art");
        int vis = showArt ? View.VISIBLE : View.GONE;
        undoBtn.setVisibility(vis); redoBtn.setVisibility(vis); eraserBtn.setVisibility(vis);
    }

    private void setEraser(boolean on) {
        eraser = on;
        art.setEraser(on);
        setActive(eraserBtn, on);
    }

    // Connected only while PhoneLink is on screen, so the PC turns the phone's own
    // 3-finger gestures back on as soon as you leave the app.
    @Override
    protected void onStart() {
        super.onStart();
        clipboardManager.addPrimaryClipChangedListener(clipListener);
        link.start();
    }

    @Override
    protected void onStop() {
        clipboardManager.removePrimaryClipChangedListener(clipListener);
        if (artMode) art.release();
        link.stop();
        super.onStop();
    }

    private SharedPreferences prefs() { return getSharedPreferences("phonelink", MODE_PRIVATE); }

    /** The PC app sends the pairing token by starting this screen through adb. */
    private void readToken(Intent i) {
        if (i == null) return;
        String t = i.getStringExtra("token");
        if (t != null && t.matches("[0-9A-Fa-f]{16,64}")) {
            prefs().edit().putString("token", t).apply();
            link.setToken(t);
        }
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        readToken(intent);
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) {
            hideBars();
            handler.postDelayed(this::sendClipboardIfNew, 300);
        }
    }

    private void hideBars() {
        WindowInsetsController c = getWindow().getInsetsController();
        if (c != null) {
            c.hide(WindowInsets.Type.systemBars());
            c.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
        }
        root.setSystemUiVisibility(View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
    }
}
