package gg.armoci.fabric;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Properties;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

final class HudSettings {
    private static final Logger LOGGER = LogManager.getLogger("Armoci Fabric Preview");
    private final Path path;
    boolean enabled = true;
    boolean coords = true;
    boolean vertical = false;
    boolean fps = true;
    boolean clock = true;
    boolean date = true;
    boolean clock24 = true;
    boolean keys = true;
    int x = 6;
    int y = 6;

    HudSettings(Path path) {
        this.path = path;
    }

    void load() {
        if (!Files.exists(path)) {
            save();
            return;
        }
        Properties p = new Properties();
        try (InputStream in = Files.newInputStream(path)) {
            p.load(in);
            enabled = flag(p, "enabled", enabled);
            coords = flag(p, "coords", coords);
            vertical = flag(p, "coords.vertical", vertical);
            fps = flag(p, "fps", fps);
            clock = flag(p, "clock", clock);
            date = flag(p, "clock.date", date);
            clock24 = flag(p, "clock.24hour", clock24);
            keys = flag(p, "keystrokes", keys);
            x = number(p, "x", x);
            y = number(p, "y", y);
        } catch (IOException | IllegalArgumentException ex) {
            LOGGER.warn("Could not read HUD settings; using available defaults", ex);
        }
    }

    private static boolean flag(Properties p, String k, boolean fallback) {
        String value = p.getProperty(k);
        return "true".equalsIgnoreCase(value) ? true : "false".equalsIgnoreCase(value) ? false : fallback;
    }

    private static int number(Properties p, String k, int fallback) {
        try {
            return Math.max(0, Math.min(4096, Integer.parseInt(p.getProperty(k, Integer.toString(fallback)))));
        } catch (NumberFormatException ex) {
            return fallback;
        }
    }

    void save() {
        Properties p = new Properties();
        p.setProperty("enabled", Boolean.toString(enabled));
        p.setProperty("coords", Boolean.toString(coords));
        p.setProperty("coords.vertical", Boolean.toString(vertical));
        p.setProperty("fps", Boolean.toString(fps));
        p.setProperty("clock", Boolean.toString(clock));
        p.setProperty("clock.date", Boolean.toString(date));
        p.setProperty("clock.24hour", Boolean.toString(clock24));
        p.setProperty("keystrokes", Boolean.toString(keys));
        p.setProperty("x", Integer.toString(x));
        p.setProperty("y", Integer.toString(y));
        try {
            Files.createDirectories(path.getParent());
            try (OutputStream out = Files.newOutputStream(path)) {
                p.store(out, "Armoci Fabric preview HUD settings");
            }
        } catch (IOException ex) {
            LOGGER.warn("Could not save HUD settings", ex);
        }
    }
}
