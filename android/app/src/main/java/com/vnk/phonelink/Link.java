package com.vnk.phonelink;

import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.TimeUnit;

/**
 * Connection to the PC app. "adb reverse" makes 127.0.0.1:7333 on the phone reach the PC over USB.
 * Sends one text command per line. Reconnects by itself.
 */
public class Link {
    public interface Listener {
        void onState(boolean connected);
        /** PC screen size in pixels (sent by the PC after connecting). */
        default void onScreen(int width, int height) { }
        /** Text copied on the PC. */
        default void onClipboard(String text) { }
    }

    private final LinkedBlockingQueue<String> queue = new LinkedBlockingQueue<>(2000);
    private volatile boolean running = true;
    private volatile boolean connected = false;
    private volatile String token = "";
    private final Listener listener;
    private Thread thread;
    private volatile int generation = 0;

    public Link(Listener listener) { this.listener = listener; }

    public void setToken(String t) { token = t == null ? "" : t; reconnect(); }

    public boolean isConnected() { return connected; }

    public void send(String line) {
        if (connected) queue.offer(line);
    }

    public void start() {
        running = true;
        final int gen = ++generation;
        thread = new Thread(() -> loop(gen), "PhoneLink");
        thread.start();
    }

    public void stop() {
        running = false;
        generation++;
        if (thread != null) thread.interrupt();
        reconnect();
    }

    private volatile Socket current;

    private void reconnect() {
        Socket s = current;
        if (s != null) try { s.close(); } catch (Exception ignored) {}
    }

    private void setConnected(boolean c) {
        if (connected != c) { connected = c; listener.onState(c); }
    }

    private void loop(int gen) {
        while (running && gen == generation) {
            try (Socket s = new Socket()) {
                current = s;
                s.setTcpNoDelay(true);
                s.connect(new InetSocketAddress("127.0.0.1", 7333), 1000);
                OutputStream out = s.getOutputStream();
                out.write(("H " + token + "\n").getBytes(StandardCharsets.UTF_8));
                out.flush();
                queue.clear();
                setConnected(true);
                // Read messages from the PC (screen size, later clipboard)
                final java.io.InputStream in = s.getInputStream();
                Thread reader = new Thread(() -> {
                    try (java.io.BufferedReader r = new java.io.BufferedReader(
                            new java.io.InputStreamReader(in, StandardCharsets.UTF_8))) {
                        String msg;
                        while ((msg = r.readLine()) != null) {
                            String[] p = msg.split(" ");
                            if (p.length == 3 && p[0].equals("SCR")) {
                                listener.onScreen(Integer.parseInt(p[1]), Integer.parseInt(p[2]));
                            } else if (p.length == 2 && p[0].equals("CB")) {
                                listener.onClipboard(new String(android.util.Base64.decode(p[1], android.util.Base64.DEFAULT), StandardCharsets.UTF_8));
                            }
                        }
                    } catch (Exception ignored) { }
                }, "PhoneLinkReader");
                reader.setDaemon(true);
                reader.start();
                StringBuilder batch = new StringBuilder();
                long lastPing = System.currentTimeMillis();
                while (running && gen == generation) {
                    String line = queue.poll(500, TimeUnit.MILLISECONDS);
                    if (line == null) {
                        if (System.currentTimeMillis() - lastPing > 1000) {
                            out.write("P\n".getBytes(StandardCharsets.UTF_8));
                            out.flush();
                            lastPing = System.currentTimeMillis();
                        }
                        continue;
                    }
                    batch.setLength(0);
                    batch.append(line).append('\n');
                    String more;
                    while ((more = queue.poll()) != null) batch.append(more).append('\n');
                    out.write(batch.toString().getBytes(StandardCharsets.UTF_8));
                    out.flush();
                }
            } catch (InterruptedException e) {
                break;
            } catch (Exception e) {
                // PC app not running or cable unplugged: try again soon
            } finally {
                if (gen == generation || !running) { current = null; setConnected(false); }
            }
            try { Thread.sleep(800); } catch (InterruptedException e) { break; }
        }
    }
}
