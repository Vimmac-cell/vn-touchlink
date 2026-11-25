package com.vnk.phonelink;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.view.HapticFeedbackConstants;
import android.view.MotionEvent;
import android.view.View;

import java.util.Arrays;
import java.util.Locale;

/**
 * Art pad: the phone works like a drawing tablet. The bright rectangle = the whole PC screen.
 *
 * 1 finger      : draw (pressure from how flat the finger presses)
 * 2-finger tap  : undo        3-finger tap : redo
 * 2-finger drag : move canvas  pinch       : zoom canvas
 * Very big touches (palm) are ignored.
 */
public class ArtPadView extends View {

    private final Link link;
    private final float dp, mmToPx;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Paint areaPaint = new Paint(), borderPaint = new Paint(Paint.ANTI_ALIAS_FLAG),
            textPaint = new Paint(Paint.ANTI_ALIAS_FLAG), inkPaint = new Paint(Paint.ANTI_ALIAS_FLAG),
            barPaint = new Paint();

    private int pcW = 1920, pcH = 1080;
    private final RectF area = new RectF();

    // Touch state
    private enum Mode { IDLE, PENDING, DRAW, GESTURE, PALM }
    private Mode mode = Mode.IDLE;
    private int maxPointers;
    private long downTime;
    private float downX, downY;
    private float lastCx, lastCy, lastSpread, startCx, startCy, startSpread;
    private boolean gestureMoved, panning, zooming;
    private boolean eraser;

    // Pressure from finger contact size, self-calibrating
    private final float[] samples = new float[400];
    private int sampleCount, sampleIndex;
    private float lo = -1, hi = -1;
    private float smoothP = 0.5f;
    private float shownP;

    // Ink preview on the phone (fades)
    private final Path ink = new Path();
    private long inkTime;

    private static final long COMMIT_MS = 45, TAP_MS = 300;
    private static final float PALM_MM = 20f;
    private static final float ZOOM_K = 1.2f;

    private final Runnable commit = () -> { if (mode == Mode.PENDING) startStroke(); };

    public ArtPadView(Context ctx, Link link) {
        super(ctx);
        this.link = link;
        dp = ctx.getResources().getDisplayMetrics().density;
        mmToPx = ctx.getResources().getDisplayMetrics().xdpi / 25.4f;
        setBackgroundColor(Color.rgb(8, 8, 10));
        areaPaint.setColor(Color.rgb(22, 22, 28));
        borderPaint.setColor(Color.rgb(70, 110, 200));
        borderPaint.setStyle(Paint.Style.STROKE);
        borderPaint.setStrokeWidth(1.5f * dp);
        textPaint.setColor(Color.rgb(80, 80, 95));
        textPaint.setTextSize(13 * dp);
        textPaint.setTextAlign(Paint.Align.CENTER);
        inkPaint.setColor(Color.rgb(120, 160, 255));
        inkPaint.setStyle(Paint.Style.STROKE);
        inkPaint.setStrokeWidth(2 * dp);
        inkPaint.setStrokeCap(Paint.Cap.ROUND);
        inkPaint.setStrokeJoin(Paint.Join.ROUND);
        barPaint.setColor(Color.rgb(70, 110, 200));
    }

    public void setScreen(int w, int h) {
        if (w > 0 && h > 0) { pcW = w; pcH = h; layoutArea(); invalidate(); }
    }

    public void setEraser(boolean on) {
        eraser = on;
        link.send("PE " + (on ? "1" : "0"));
        inkPaint.setColor(on ? Color.rgb(255, 120, 120) : Color.rgb(120, 160, 255));
    }

    /** Called when leaving the art pad: lift the pen. */
    public void release() {
        handler.removeCallbacks(commit);
        if (mode == Mode.DRAW) link.send("PU " + nx(lastCx) + " " + ny(lastCy));
        if (panning) link.send("PAN e");
        panning = false;
        mode = Mode.IDLE;
        link.send("PL");
    }

    @Override
    protected void onSizeChanged(int w, int h, int ow, int oh) {
        super.onSizeChanged(w, h, ow, oh);
        layoutArea();
    }

    /** Biggest rectangle with the PC screen's shape that fits the phone. */
    private void layoutArea() {
        float pad = 6 * dp;
        float vw = getWidth() - 2 * pad, vh = getHeight() - 2 * pad;
        if (vw <= 0 || vh <= 0) return;
        float aspect = (float) pcW / pcH;
        float w = vw, h = vw / aspect;
        if (h > vh) { h = vh; w = vh * aspect; }
        float l = (getWidth() - w) / 2f, t = (getHeight() - h) / 2f;
        area.set(l, t, l + w, t + h);
    }

    @Override
    protected void onDraw(Canvas c) {
        super.onDraw(c);
        c.drawRect(area, areaPaint);
        c.drawRect(area, borderPaint);
        if (mode == Mode.IDLE && ink.isEmpty()) {
            c.drawText("Art pad - this box is your PC screen", area.centerX(), area.centerY() - 10 * dp, textPaint);
            c.drawText("2-finger tap: undo   3-finger tap: redo   2 fingers: move / zoom", area.centerX(), area.centerY() + 12 * dp, textPaint);
        }
        // fade the ink preview
        long age = SystemClock.uptimeMillis() - inkTime;
        if (!ink.isEmpty()) {
            if (mode != Mode.DRAW && age > 1200) ink.reset();
            else { c.drawPath(ink, inkPaint); if (mode != Mode.DRAW) postInvalidateDelayed(100); }
        }
        // pressure bar on the right
        if (mode == Mode.DRAW) {
            float h = area.height() * shownP;
            c.drawRect(getWidth() - 4 * dp, area.bottom - h, getWidth(), area.bottom, barPaint);
        }
    }

    // ---------- helpers ----------

    private String nx(float x) { return String.format(Locale.US, "%.5f", (x - area.left) / area.width()); }
    private String ny(float y) { return String.format(Locale.US, "%.5f", (y - area.top) / area.height()); }
    private static String f(float v) { return String.format(Locale.US, "%.2f", v); }

    private float[] centroid(MotionEvent e, int skip) {
        float sx = 0, sy = 0; int n = 0;
        for (int i = 0; i < e.getPointerCount(); i++) { if (i == skip) continue; sx += e.getX(i); sy += e.getY(i); n++; }
        if (n == 0) return new float[]{lastCx, lastCy, lastSpread};
        float cx = sx / n, cy = sy / n, s = 0;
        for (int i = 0; i < e.getPointerCount(); i++) { if (i == skip) continue; s += (float) Math.hypot(e.getX(i) - cx, e.getY(i) - cy); }
        return new float[]{cx, cy, s / n};
    }

    /** Pressure 0..1 from finger contact size, calibrated to your own fingers over time. */
    private float pressure(MotionEvent e, int idx, int hist) {
        float size = hist < 0 ? e.getTouchMajor(idx) : e.getHistoricalTouchMajor(idx, hist);
        if (size <= 0) size = (hist < 0 ? e.getPressure(idx) : e.getHistoricalPressure(idx, hist)) * 100f;
        samples[sampleIndex] = size;
        sampleIndex = (sampleIndex + 1) % samples.length;
        if (sampleCount < samples.length) sampleCount++;
        if (sampleCount >= 30 && (sampleIndex % 20 == 0 || lo < 0)) {
            float[] s = Arrays.copyOf(samples, sampleCount);
            Arrays.sort(s);
            lo = s[(int) (sampleCount * 0.05f)];
            hi = s[(int) (sampleCount * 0.95f)];
        }
        float p;
        if (lo < 0 || hi - lo < 0.5f) p = 0.5f;
        else p = Math.max(0f, Math.min(1f, (size - lo) / (hi - lo)));
        smoothP += (p - smoothP) * 0.35f;
        shownP = smoothP;
        return smoothP;
    }

    private void startStroke() {
        mode = Mode.DRAW;
        link.send("PD " + nx(downX) + " " + ny(downY) + " " + f(smoothP));
        ink.reset();
        ink.moveTo(downX, downY);
        inkTime = SystemClock.uptimeMillis();
        lastCx = downX; lastCy = downY;
    }

    // ---------- touch ----------

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        long now = SystemClock.uptimeMillis();
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN: {
                maxPointers = 1;
                downTime = now;
                downX = e.getX(); downY = e.getY();
                lastCx = downX; lastCy = downY;
                if (e.getTouchMajor(0) / mmToPx > PALM_MM) { mode = Mode.PALM; return true; }
                smoothP = 0.5f;
                pressure(e, 0, -1);
                mode = Mode.PENDING;
                handler.postDelayed(commit, COMMIT_MS);
                return true;
            }
            case MotionEvent.ACTION_POINTER_DOWN: {
                maxPointers = Math.max(maxPointers, e.getPointerCount());
                handler.removeCallbacks(commit);
                if (mode == Mode.DRAW) link.send("PU " + nx(lastCx) + " " + ny(lastCy));
                mode = Mode.GESTURE;
                gestureMoved = false;
                float[] c = centroid(e, -1);
                lastCx = startCx = c[0]; lastCy = startCy = c[1]; lastSpread = startSpread = c[2];
                return true;
            }
            case MotionEvent.ACTION_MOVE: {
                if (mode == Mode.PALM) return true;
                if (mode == Mode.PENDING) {
                    if (e.getTouchMajor(0) / mmToPx > PALM_MM) { handler.removeCallbacks(commit); mode = Mode.PALM; return true; }
                    if (Math.hypot(e.getX() - downX, e.getY() - downY) > 3 * dp) { handler.removeCallbacks(commit); startStroke(); }
                    else { pressure(e, 0, -1); return true; }
                }
                if (mode == Mode.DRAW) {
                    // send every in-between point Android collected for smoother lines
                    for (int h = 0; h < e.getHistorySize(); h++) {
                        float x = e.getHistoricalX(0, h), y = e.getHistoricalY(0, h);
                        link.send("PM " + nx(x) + " " + ny(y) + " " + f(pressure(e, 0, h)));
                        ink.lineTo(x, y);
                    }
                    float x = e.getX(), y = e.getY();
                    link.send("PM " + nx(x) + " " + ny(y) + " " + f(pressure(e, 0, -1)));
                    ink.lineTo(x, y);
                    lastCx = x; lastCy = y;
                    inkTime = SystemClock.uptimeMillis();
                    invalidate();
                } else if (mode == Mode.GESTURE && e.getPointerCount() == 2 && maxPointers == 2) {
                    float[] c = centroid(e, -1);
                    float tx = c[0] - startCx, ty = c[1] - startCy, ts = c[2] - startSpread;
                    if (!panning && !zooming && Math.max(Math.hypot(tx, ty), Math.abs(ts)) > 10 * dp) {
                        gestureMoved = true;
                        if (Math.abs(ts) > Math.hypot(tx, ty)) zooming = true;
                        else { panning = true; link.send("PAN s"); }
                    }
                    if (panning) {
                        float scale = pcW / area.width();   // canvas follows your fingers 1:1 on the PC screen
                        link.send("PAN m " + f((c[0] - lastCx) * scale) + " " + f((c[1] - lastCy) * scale));
                    } else if (zooming) {
                        link.send("ZA " + f((c[2] - lastSpread) * ZOOM_K));
                    }
                    lastCx = c[0]; lastCy = c[1]; lastSpread = c[2];
                } else if (mode == Mode.GESTURE) {
                    float[] c = centroid(e, -1);
                    if (Math.hypot(c[0] - startCx, c[1] - startCy) > 12 * dp) gestureMoved = true;
                }
                return true;
            }
            case MotionEvent.ACTION_POINTER_UP: {
                float[] c = centroid(e, e.getActionIndex());
                lastCx = c[0]; lastCy = c[1]; lastSpread = c[2];
                if (panning) { link.send("PAN e"); panning = false; }
                zooming = false;
                return true;
            }
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL: {
                handler.removeCallbacks(commit);
                if (mode == Mode.PENDING && e.getActionMasked() == MotionEvent.ACTION_UP) {
                    startStroke();                       // a quick tap = a dot
                    link.send("PU " + nx(downX) + " " + ny(downY));
                } else if (mode == Mode.DRAW) {
                    link.send("PU " + nx(e.getX()) + " " + ny(e.getY()));
                } else if (mode == Mode.GESTURE && !gestureMoved && now - downTime < TAP_MS) {
                    if (maxPointers == 2) { link.send("G art_tap2"); performHapticFeedback(HapticFeedbackConstants.KEYBOARD_TAP); }
                    else if (maxPointers == 3) { link.send("G art_tap3"); performHapticFeedback(HapticFeedbackConstants.KEYBOARD_TAP); }
                }
                if (panning) link.send("PAN e");
                panning = false; zooming = false;
                mode = Mode.IDLE;
                inkTime = SystemClock.uptimeMillis();
                invalidate();
                return true;
            }
        }
        return super.onTouchEvent(e);
    }
}
