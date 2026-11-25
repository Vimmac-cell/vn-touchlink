package com.vnk.phonelink;

/** Public links. Change DOWNLOAD_URL here if the PC app moves. */
public final class AppInfo {
    private AppInfo() {}

    /** Where people download the PC app (shown as link + QR code while not connected). */
    public static final String DOWNLOAD_URL = "https://github.com/Vimmac-cell/vn-touchlink/releases/latest";

    /** Short form shown as text under the QR code. */
    public static final String DOWNLOAD_SHORT = "github.com/Vimmac-cell/vn-touchlink";
}
