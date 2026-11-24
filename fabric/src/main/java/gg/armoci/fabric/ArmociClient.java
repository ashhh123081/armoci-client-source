package gg.armoci.fabric;

import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.Locale;
import net.fabricmc.api.ClientModInitializer;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;
import net.fabricmc.fabric.api.client.keybinding.v1.KeyBindingHelper;
import net.fabricmc.fabric.api.client.rendering.v1.HudRenderCallback;
import net.fabricmc.loader.api.FabricLoader;
import net.minecraft.client.MinecraftClient;
import net.minecraft.client.gui.DrawableHelper;
import net.minecraft.client.option.KeyBinding;
import net.minecraft.client.util.InputUtil;
import net.minecraft.client.util.math.MatrixStack;
import org.lwjgl.glfw.GLFW;

/** Client-side HUD migration; uses the supported Fabric lifecycle rather than a native injector. */
public final class ArmociClient implements ClientModInitializer {
    private static final DateTimeFormatter TIME_24 = DateTimeFormatter.ofPattern("HH:mm");
    private static final DateTimeFormatter TIME_12 = DateTimeFormatter.ofPattern("hh:mm a", Locale.ROOT);
    private static final DateTimeFormatter DATE = DateTimeFormatter.ofPattern("yyyy-MM-dd");
    private HudSettings settings;
    private KeyBinding toggle;
    private KeyBinding reload;
    private KeyBinding configure;

    @Override
    public void onInitializeClient() {
        settings = new HudSettings(FabricLoader.getInstance().getConfigDir().resolve("armoci-fabric.properties"));
        settings.load();
        toggle = KeyBindingHelper.registerKeyBinding(new KeyBinding(
                "key.armoci.toggle", InputUtil.Type.KEYSYM, GLFW.GLFW_KEY_F8, "category.armoci"));
        reload = KeyBindingHelper.registerKeyBinding(new KeyBinding(
                "key.armoci.reload", InputUtil.Type.KEYSYM, GLFW.GLFW_KEY_UNKNOWN, "category.armoci"));
        configure = KeyBindingHelper.registerKeyBinding(new KeyBinding(
                "key.armoci.configure", InputUtil.Type.KEYSYM, GLFW.GLFW_KEY_F7, "category.armoci"));
        ClientTickEvents.END_CLIENT_TICK.register(client -> {
            while (toggle.wasPressed()) {
                settings.enabled = !settings.enabled;
                settings.save();
            }
            while (configure.wasPressed()) {
                if (client.currentScreen == null) client.openScreen(new HudConfigScreen(settings));
            }
            while (reload.wasPressed()) {
                settings.load();
            }
        });
        HudRenderCallback.EVENT.register(this::render);
    }

    private int line(MinecraftClient client, MatrixStack matrices, String text, int x, int y) {
        int width = client.textRenderer.getWidth(text);
        DrawableHelper.fill(matrices, x, y, x + width + 8, y + 13, 0x990E1420);
        client.textRenderer.drawWithShadow(matrices, text, x + 4, y + 2, 0xFFFFFF);
        return y + 15;
    }

    private void key(MinecraftClient client, MatrixStack matrices, String label, boolean pressed,
                     int x, int y, int width) {
        DrawableHelper.fill(matrices, x, y, x + width, y + 17, pressed ? 0xE0FFFFFF : 0x990E1420);
        int textX = x + (width - client.textRenderer.getWidth(label)) / 2;
        client.textRenderer.draw(matrices, label, textX, y + 4, pressed ? 0x202020 : 0xFFFFFF);
    }

    private String label(KeyBinding binding) {
        String name = binding.getBoundKeyLocalizedText().getString();
        return name.length() > 5 ? name.substring(0, 5) : name;
    }

    private void render(MatrixStack matrices, float tickDelta) {
        MinecraftClient client = MinecraftClient.getInstance();
        if (!settings.enabled || client.player == null || client.world == null
                || client.options.hudHidden || client.options.debugEnabled || client.currentScreen != null) {
            return;
        }
        int x = Math.min(settings.x, Math.max(0, client.getWindow().getScaledWidth() - 130));
        int y = Math.min(settings.y, Math.max(0, client.getWindow().getScaledHeight() - 110));
        if (settings.fps) {
            String debug = client.fpsDebugString;
            int space = debug == null ? -1 : debug.indexOf(' ');
            String fps = space > 0 ? debug.substring(0, space) : "--";
            y = line(client, matrices, fps + " FPS", x, y);
        }
        if (settings.coords) {
            long px = Math.round(client.player.getX());
            long py = Math.round(client.player.getY());
            long pz = Math.round(client.player.getZ());
            if (settings.vertical) {
                y = line(client, matrices, "X: " + px, x, y);
                y = line(client, matrices, "Y: " + py, x, y);
                y = line(client, matrices, "Z: " + pz, x, y);
            } else {
                y = line(client, matrices, "XYZ: " + px + " / " + py + " / " + pz, x, y);
            }
        }
        if (settings.clock) {
            LocalDateTime now = LocalDateTime.now();
            String time = now.format(settings.clock24 ? TIME_24 : TIME_12);
            if (settings.date) time += "  " + now.format(DATE);
            y = line(client, matrices, time, x, y);
        }
        if (settings.keys) {
            KeyBinding forward = client.options.keyForward;
            KeyBinding left = client.options.keyLeft;
            KeyBinding back = client.options.keyBack;
            KeyBinding right = client.options.keyRight;
            KeyBinding jump = client.options.keyJump;
            key(client, matrices, label(forward), forward.isPressed(), x + 35, y, 33);
            key(client, matrices, label(left), left.isPressed(), x, y + 19, 33);
            key(client, matrices, label(back), back.isPressed(), x + 35, y + 19, 33);
            key(client, matrices, label(right), right.isPressed(), x + 70, y + 19, 33);
            key(client, matrices, label(jump), jump.isPressed(), x, y + 38, 103);
        }
    }
}
