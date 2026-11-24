package gg.armoci.fabric;

import java.util.function.BooleanSupplier;
import net.minecraft.client.gui.screen.Screen;
import net.minecraft.client.gui.widget.ButtonWidget;
import net.minecraft.client.util.math.MatrixStack;
import net.minecraft.text.LiteralText;

/** Simple native Fabric-compatible settings screen. No recovered client dependencies. */
final class HudConfigScreen extends Screen {
    private final HudSettings settings;

    HudConfigScreen(HudSettings settings) {
        super(new LiteralText("Armoci HUD settings"));
        this.settings = settings;
    }

    private void option(String label, BooleanSupplier value, Runnable toggle, int x, int y, int w) {
        addButton(new ButtonWidget(x, y, w, 20,
                new LiteralText(label + ": " + (value.getAsBoolean() ? "ON" : "OFF")), button -> {
            toggle.run();
            button.setMessage(new LiteralText(label + ": " + (value.getAsBoolean() ? "ON" : "OFF")));
            settings.save();
        }));
    }

    @Override
    protected void init() {
        int gap = 4;
        int w = Math.min(150, (width - 24 - gap) / 2);
        int x = (width - 2 * w - gap) / 2;
        int r = x + w + gap;
        int y = Math.max(26, (height - 142) / 2);
        option("HUD", () -> settings.enabled, () -> settings.enabled = !settings.enabled, x, y, w);
        option("FPS", () -> settings.fps, () -> settings.fps = !settings.fps, r, y, w);
        option("Coordinates", () -> settings.coords, () -> settings.coords = !settings.coords, x, y + 24, w);
        option("Vertical XYZ", () -> settings.vertical, () -> settings.vertical = !settings.vertical, r, y + 24, w);
        option("Clock", () -> settings.clock, () -> settings.clock = !settings.clock, x, y + 48, w);
        option("Show date", () -> settings.date, () -> settings.date = !settings.date, r, y + 48, w);
        option("24-hour time", () -> settings.clock24, () -> settings.clock24 = !settings.clock24, x, y + 72, w);
        option("Keystrokes", () -> settings.keys, () -> settings.keys = !settings.keys, r, y + 72, w);
        addButton(new ButtonWidget(x, y + 100, 2 * w + gap, 20, new LiteralText("Done"), button -> onClose()));
    }

    @Override
    public void render(MatrixStack matrices, int mouseX, int mouseY, float delta) {
        renderBackground(matrices);
        drawCenteredText(matrices, textRenderer, title, width / 2, Math.max(6, (height - 142) / 2 - 17), 0xFFFFFF);
        super.render(matrices, mouseX, mouseY, delta);
    }

    @Override
    public boolean isPauseScreen() {
        return false;
    }
}
