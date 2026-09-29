using System.Drawing;
using VoiceToText.Services;
using Xunit;

namespace VoiceToText.Tests;

public class OverlayPositioningTests
{
    private static readonly Rectangle PrimaryWorkArea = new(0, 0, 1920, 1080);

    private static readonly OverlayPositioning.ScreenInfo Laptop = new("\\\\.\\DISPLAY1", new Rectangle(0, 0, 1920, 1080));
    private static readonly OverlayPositioning.ScreenInfo BigScreen = new("\\\\.\\DISPLAY2", new Rectangle(1920, 0, 2560, 1440));
    private static readonly OverlayPositioning.ScreenInfo ThirdScreen = new("\\\\.\\DISPLAY3", new Rectangle(4480, 0, 1920, 1080));

    [Fact]
    public void NoSavedPosition_FallsBackToPrimaryCorner()
    {
        var (left, top) = OverlayPositioning.ResolvePosition(null, null, null, new[] { Laptop }, PrimaryWorkArea);

        Assert.Equal(PrimaryWorkArea.Right - 160, left);
        Assert.Equal(PrimaryWorkArea.Bottom - 80, top);
    }

    [Fact]
    public void PreferredScreenStillConnected_KeepsExactPosition()
    {
        // Position mémorisée sur le grand écran 32" (DISPLAY2), toujours branché.
        var (left, top) = OverlayPositioning.ResolvePosition(
            2500, 900, BigScreen.DeviceName, new[] { Laptop, BigScreen, ThirdScreen }, PrimaryWorkArea);

        Assert.Equal(2500, left);
        Assert.Equal(900, top);
    }

    [Fact]
    public void PreferredScreenDisconnected_FallsBackToPrimaryCorner()
    {
        // Le grand écran (DISPLAY2) a été débranché (ex. portable emmené au travail) : seul le
        // laptop reste. La position mémorisée dessus ne doit plus être utilisée telle quelle.
        var (left, top) = OverlayPositioning.ResolvePosition(
            2500, 900, BigScreen.DeviceName, new[] { Laptop }, PrimaryWorkArea);

        Assert.Equal(PrimaryWorkArea.Right - 160, left);
        Assert.Equal(PrimaryWorkArea.Bottom - 80, top);
    }

    [Fact]
    public void PreferredScreenReconnected_SnapsBackToRememberedPosition()
    {
        // Séquence complète : position prise sur DISPLAY2, puis débranché (repli), puis rebranché.
        var whileConnected = OverlayPositioning.ResolvePosition(
            2500, 900, BigScreen.DeviceName, new[] { Laptop, BigScreen }, PrimaryWorkArea);
        Assert.Equal((2500d, 900d), whileConnected);

        var whileDisconnected = OverlayPositioning.ResolvePosition(
            2500, 900, BigScreen.DeviceName, new[] { Laptop }, PrimaryWorkArea);
        Assert.NotEqual((2500d, 900d), whileDisconnected);

        // On continue à passer la position ORIGINALE mémorisée (2500,900) — pas le repli — pour
        // simuler OverlayWindow qui réapplique ses champs _preferred* à chaque changement d'écran.
        var afterReconnect = OverlayPositioning.ResolvePosition(
            2500, 900, BigScreen.DeviceName, new[] { Laptop, BigScreen }, PrimaryWorkArea);
        Assert.Equal((2500d, 900d), afterReconnect);
    }

    [Fact]
    public void SavedPositionWithoutScreenName_ButStillOnAScreen_IsKept()
    {
        // Réglages migrés depuis une version antérieure au suivi par écran (pas de DeviceName
        // enregistré) : si la position brute tombe quand même sur un écran connecté, on la garde.
        var (left, top) = OverlayPositioning.ResolvePosition(
            100, 100, null, new[] { Laptop }, PrimaryWorkArea);

        Assert.Equal(100, left);
        Assert.Equal(100, top);
    }

    [Fact]
    public void SavedPositionOffAllScreens_FallsBackEvenWithScreenNameMatch()
    {
        // Cas limite : l'écran mémorisé est bien reconnecté, mais avec une résolution plus petite
        // qui ne contient plus l'ancienne position (ex. changement de résolution).
        var shrunkBigScreen = new OverlayPositioning.ScreenInfo(BigScreen.DeviceName, new Rectangle(1920, 0, 640, 480));

        var (left, top) = OverlayPositioning.ResolvePosition(
            2500, 900, BigScreen.DeviceName, new[] { Laptop, shrunkBigScreen }, PrimaryWorkArea);

        Assert.Equal(PrimaryWorkArea.Right - 160, left);
        Assert.Equal(PrimaryWorkArea.Bottom - 80, top);
    }
}
