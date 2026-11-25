package com.vnk.phonelink;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Rect;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.view.HapticFeedbackConstants;
import android.view.MotionEvent;
import android.view.View;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * Full-screen touchpad. Turns finger movement into commands for the PC app.
 *
 * 1 finger : move pointer, tap = left click, tap then drag = drag, double tap = double click
 * 2 fingers: drag = scroll, pinch = zoom, tap = right click
 * 3 fingers: swipe up/down = gesture, swipe left/right = live app switcher, tap = gesture
 * 4 fingers: swipe any way = gesture, tap = gesture
 */
public class TouchpadView extends View {

    private enum Mode { NONE, MOVE, DRAG, SCROLL, ZOOM, ALTTAB, DONE }

    private final Link link;
    private final float dp;
    private final float slop, swipeDist, altStep;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Paint text = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint dot = new Paint(Paint.ANTI_ALIAS_FLAG);

    // Tuning
    private static final long TAP_MS = 250, MULTI_TAP_MS = 350, CLICK_DELAY_MS = 180;
    private static final float SCROLL_K = 2.0f;   // wheel units per phone pixel (120 = one notch)
    private static final float ZOOM_K = 1.2f;

    private Mode mode = Mode.NONE;
    private int maxPointers;
    private long downTime;
    private boolean moved;
    private float startX, startY;          // first finger start
    private float lastCx, lastCy, lastSpread;
    private float multiStartCx, multiStartCy, multiStartSpread;
    private long lastMoveTime;
    private float altBase;
    private boolean dragging;
    private boolean secondTap;              // this touch began right after a tap
    private Runnable pendingClick;
    private float lastTapX, lastTapY;
    private boolean connected;

    public TouchpadView(Context ctx, Link link) {
        super(ctx);
        this.link = link;
        dp = ctx.getResources().getDisplayMetrics().density;
        slop = 8 * dp;
        swipeDist = 50 * dp;
        altStep = 70 * dp;
        setBackgroundColor(Color.BLACK);
        setKeepScreenOn(true);
        text.setColor(Color.rgb(90, 90, 100));
        text.setTextSize(15 * dp);
        text.setTextAlign(Paint.Align.CENTER);
        setHapticFeedbackEnabled(true);
    }

    public void setConnected(boolean c) {
        connected = c;
        invalidate();
    }

    @Override
    protected void onLayout(boolean changed, int l, int t, int r, int b) {
        super.onLayout(changed, l, t, r, b);
        // Ask Android not to treat edge swipes as "Back" (the system limits how tall this can be)
        List<Rect> rects = new ArrayList<>();
        int h = getHeight(), w = getWidth(), band = (int) (40 * dp), tall = (int) (200 * dp);
        int top = Math.max(0, h / 2 - tall / 2);
        rects.add(new Rect(0, top, band, top + tall));
        rects.add(new Rect(w - band, top, w, top + tall));
        setSystemGestureExclusionRects(rects);
    }

    // ----- "Get the PC app" QR code (shown while not connected) -----
    private android.graphics.Bitmap qr;
    private final android.graphics.RectF qrRect = new android.graphics.RectF();
    private final Paint linkPaint = new Paint(Paint.ANTI_ALIAS_FLAG);

    private android.graphics.Bitmap makeQr(int size) {
        try {
            com.google.zxing.common.BitMatrix m = new com.google.zxing.qrcode.QRCodeWriter()
                    .encode(AppInfo.DOWNLOAD_URL, com.google.zxing.BarcodeFormat.QR_CODE, size, size);
            int[] px = new int[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = m.get(x, y) ? Color.BLACK : Color.WHITE;
            return android.graphics.Bitmap.createBitmap(px, size, size, android.graphics.Bitmap.Config.ARGB_8888);
        } catch (Exception e) {
            return null;
        }
    }

    private void openDownloadPage() {
        try {
            getContext().startActivity(new android.content.Intent(android.content.Intent.ACTION_VIEW,
                    android.net.Uri.parse(AppInfo.DOWNLOAD_URL)));
        } catch (Exception ignored) { }
    }

    @Override
    protected void onDraw(Canvas c) {
        super.onDraw(c);
        if (connected) {
            float x = getWidth() / 2f, y = getHeight() / 2f;
            dot.setColor(Color.rgb(40, 160, 80));
            c.drawCircle(x, y - 40 * dp, 6 * dp, dot);
            c.drawText("Connected to PC", x, y, text);
            return;
        }
        // Not connected: steps on the left, QR code to the PC app on the right
        float qrSize = Math.min(getHeight() * 0.55f, 170 * dp);
        float x = getWidth() * 0.36f, y = getHeight() / 2f - 50 * dp;
        dot.setColor(Color.rgb(170, 60, 50));
        c.drawCircle(x, y - 30 * dp, 6 * dp, dot);
        c.drawText("Waiting for PC...", x, y, text);
        c.drawText("1. Install VN TouchLink on your Windows PC", x, y + 34 * dp, text);
        c.drawText("2. Turn on USB debugging (the PC app shows how)", x, y + 58 * dp, text);
        c.drawText("3. Plug in USB and tap Allow", x, y + 82 * dp, text);

        float qx = getWidth() * 0.78f - qrSize / 2f, qy = getHeight() / 2f - qrSize / 2f - 12 * dp;
        qrRect.set(qx, qy, qx + qrSize, qy + qrSize);
        if (qr == null || qr.getWidth() != (int) qrSize) qr = makeQr((int) qrSize);
        if (qr != null) c.drawBitmap(qr, qx, qy, null);
        linkPaint.setColor(Color.rgb(110, 150, 230));
        linkPaint.setTextSize(13 * dp);
        linkPaint.setTextAlign(Paint.Align.CENTER);
        c.drawText("Get the PC app:", qrRect.centerX(), qrRect.bottom + 20 * dp, text);
        c.drawText(AppInfo.DOWNLOAD_SHORT, qrRect.centerX(), qrRect.bottom + 40 * dp, linkPaint);
    }

    // ---------- helpers ----------

    private void send(String s) { link.send(s); }

    private static String f(float v) { return String.format(Locale.US, "%.2f", v); }

    private void haptic() { performHapticFeedback(HapticFeedbackConstants.KEYBOARD_TAP); }

    /** Centroid of all fingers, leaving out the one at index skip (or -1). */
    private float[] centroid(MotionEvent e, int skip) {
        float sx = 0, sy = 0; int n = 0;
        for (int i = 0; i < e.getPointerCount(); i++) {
            if (i == skip) continue;
            sx += e.getX(i); sy += e.getY(i); n++;
        }
        if (n == 0) return new float[]{lastCx, lastCy, 0};
        float cx = sx / n, cy = sy / n, spread = 0;
        for (int i = 0; i < e.getPointerCount(); i++) {
            if (i == skip) continue;
            spread += (float) Math.hypot(e.getX(i) - cx, e.getY(i) - cy);
        }
        return new float[]{cx, cy, spread / n};
    }

    private void rebase(MotionEvent e, int skip, boolean resetMulti) {
        float[] c = centroid(e, skip);
        lastCx = c[0]; lastCy = c[1]; lastSpread = c[2];
        if (resetMulti) { multiStartCx = c[0]; multiStartCy = c[1]; multiStartSpread = c[2]; }
    }

    private void cancelPendingClick() {
        if (pendingClick != null) { handler.removeCallbacks(pendingClick); pendingClick = null; }
    }

    // ---------- touch handling ----------

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        long now = SystemClock.uptimeMillis();
        if (!connected) {
            // Tap the QR code or link to open the download page
            if (e.getActionMasked() == MotionEvent.ACTION_UP) {
                android.graphics.RectF hit = new android.graphics.RectF(qrRect);
                hit.bottom += 50 * dp;
                if (hit.contains(e.getX(), e.getY())) openDownloadPage();
            }
            return true;
        }
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN: {
                mode = Mode.NONE;
                maxPointers = 1;
                downTime = now;
                lastMoveTime = now;
                moved = false;
                startX = e.getX(); startY = e.getY();
                rebase(e, -1, true);
                // A tap just happened nearby and its click is still waiting? Then this may be tap-and-drag or double tap.
                secondTap = pendingClick != null && Math.hypot(startX - lastTapX, startY - lastTapY) < 60 * dp;
                if (secondTap) cancelPendingClick();
                else if (pendingClick != null) { handler.removeCallbacks(pendingClick); pendingClick.run(); pendingClick = null; }
                return true;
            }
            case MotionEvent.ACTION_POINTER_DOWN: {
                maxPointers = Math.max(maxPointers, e.getPointerCount());
                if (secondTap && !dragging) { send("C L"); secondTap = false; } // treat as a finished click
                if (mode == Mode.MOVE || mode == Mode.NONE) mode = Mode.NONE;
                rebase(e, -1, true);
                return true;
            }
            case MotionEvent.ACTION_MOVE: {
                onMove(e, now);
                return true;
            }
            case MotionEvent.ACTION_POINTER_UP: {
                if (mode == Mode.ALTTAB && e.getPointerCount() - 1 < 3) { send("A end"); mode = Mode.DONE; }
                rebase(e, e.getActionIndex(), false);
                return true;
            }
            case MotionEvent.ACTION_UP: {
                onUp(now);
                return true;
            }
            case MotionEvent.ACTION_CANCEL: {
                if (dragging) { send("U L"); dragging = false; }
                if (mode == Mode.ALTTAB) send("A end");
                mode = Mode.NONE;
                return true;
            }
        }
        return super.onTouchEvent(e);
    }

    private void onMove(MotionEvent e, long now) {
        int n = e.getPointerCount();
        float[] c = centroid(e, -1);
        float cx = c[0], cy = c[1], spread = c[2];
        float dx = cx - lastCx, dy = cy - lastCy;
        float dSpread = spread - lastSpread;
        long dt = Math.max(1, now - lastMoveTime);

        if (maxPointers == 1 && n == 1) {
            if (!moved && Math.hypot(cx - startX, cy - startY) > slop) {
                moved = true;
                if (secondTap) { send("D L"); dragging = true; mode = Mode.DRAG; haptic(); }
                else mode = Mode.MOVE;
            }
            if (moved) {
                float speed = (float) Math.hypot(dx, dy) / dt;          // phone px per ms
                float gain = 0.9f + Math.min(2.2f, speed * 0.9f);       // pointer acceleration
                send("M " + f(dx * gain) + " " + f(dy * gain));
            }
        } else if (n == 2 && maxPointers == 2) {
            float tx = cx - multiStartCx, ty = cy - multiStartCy;
            float ts = spread - multiStartSpread;
            if (mode == Mode.NONE) {
                float travel = (float) Math.hypot(tx, ty);
                if (Math.abs(ts) > slop * 2 && Math.abs(ts) > travel) { mode = Mode.ZOOM; moved = true; }
                else if (travel > slop) { mode = Mode.SCROLL; moved = true; }
            }
            if (mode == Mode.SCROLL) {
                // Content follows fingers (Windows default): fingers down = scroll up
                send("W " + f(dy * SCROLL_K) + " " + f(-dx * SCROLL_K));
            } else if (mode == Mode.ZOOM) {
                send("Z " + f(dSpread * ZOOM_K));
            }
        } else if (n >= 3) {
            float tx = cx - multiStartCx, ty = cy - multiStartCy;
            if (mode == Mode.NONE || mode == Mode.MOVE || mode == Mode.SCROLL || mode == Mode.ZOOM) {
                if (Math.hypot(tx, ty) > slop) moved = true;
                if (Math.hypot(tx, ty) > swipeDist) {
                    boolean horizontal = Math.abs(tx) > Math.abs(ty);
                    if (n == 3 && horizontal) {
                        mode = Mode.ALTTAB;
                        altBase = cx;
                        send("A start");
                        haptic();
                    } else {
                        String dir = horizontal ? (tx > 0 ? "right" : "left") : (ty > 0 ? "down" : "up");
                        send("G swipe" + Math.min(n, 4) + "_" + dir);
                        mode = Mode.DONE;
                        haptic();
                    }
                }
            } else if (mode == Mode.ALTTAB) {
                while (cx - altBase > altStep) { send("A next"); altBase += altStep; haptic(); }
                while (altBase - cx > altStep) { send("A prev"); altBase -= altStep; haptic(); }
            }
        }

        lastCx = cx; lastCy = cy; lastSpread = spread;
        lastMoveTime = now;
    }

    private void onUp(long now) {
        long dur = now - downTime;
        if (dragging) {
            send("U L");
            dragging = false;
        } else if (mode == Mode.ALTTAB) {
            send("A end");
        } else if (!moved) {
            if (maxPointers == 1 && dur < TAP_MS) {
                if (secondTap) {
                    // double tap
                    send("C L"); send("C L");
                    haptic();
                } else {
                    // wait a moment: a second touch may turn this into tap-and-drag
                    lastTapX = startX; lastTapY = startY;
                    pendingClick = () -> { send("C L"); pendingClick = null; };
                    handler.postDelayed(pendingClick, CLICK_DELAY_MS);
                    haptic();
                }
            } else if (maxPointers == 2 && dur < MULTI_TAP_MS) {
                send("C R"); haptic();
            } else if (maxPointers == 3 && dur < MULTI_TAP_MS) {
                send("G tap3"); haptic();
            } else if (maxPointers >= 4 && dur < MULTI_TAP_MS) {
                send("G tap4"); haptic();
            }
        }
        secondTap = false;
        mode = Mode.NONE;
    }
}
