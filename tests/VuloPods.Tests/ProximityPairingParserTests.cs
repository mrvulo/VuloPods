using VuloPods.Core.Bluetooth;
using VuloPods.Core.Models;

namespace VuloPods.Tests;

public class ProximityPairingParserTests
{
    // Baut ein 27-Byte-Paket (ohne Company-ID) mit frei wählbaren Statusbytes.
    internal static byte[] Packet(ushort model, byte status, byte battery, byte charging, byte lid)
    {
        var p = new byte[27];
        p[0] = 0x07; p[1] = 0x19; p[2] = 0x01;
        p[3] = (byte)(model >> 8); p[4] = (byte)(model & 0xFF);
        p[5] = status; p[6] = battery; p[7] = charging; p[8] = lid;
        return p;
    }

    [Fact]
    public void Parses_LeftIsCurrent_BothInEar()
    {
        // status 0x2A = Bit5 (curr = links) | Bit3 (anot im Ohr) | Bit1 (curr im Ohr)
        // battery 0x78 = curr(links)=8 → 80 %, anot(rechts)=7 → 70 %
        // charging 0x05 = Case 50 %, nichts lädt
        var adv = ProximityPairingParser.Parse(Packet(0x1420, 0x2A, 0x78, 0x05, 0x00));

        Assert.NotNull(adv);
        Assert.Equal(AirPodsModel.AirPodsPro2, adv.Model);
        Assert.Equal(0x1420, adv.ModelId);
        Assert.Equal(80, adv.LeftBattery);
        Assert.Equal(70, adv.RightBattery);
        Assert.Equal(50, adv.CaseBattery);
        Assert.True(adv.LeftInEar);
        Assert.True(adv.RightInEar);
        Assert.False(adv.BothInCase);
        Assert.False(adv.LeftCharging);
        Assert.False(adv.RightCharging);
        Assert.False(adv.CaseCharging);
    }

    [Fact]
    public void Parses_RightIsCurrent_InCase_Charging_LidClosed()
    {
        // status 0x04 = Bit5 nicht gesetzt (curr = rechts), Bit2 beide im Case
        // battery 0x9A = curr(rechts)=0xA → 100 %, anot(links)=9 → 90 %
        // charging 0x7F = Case unbekannt (0xF), curr lädt, anot lädt, Case lädt
        // lid 0x0B = Bit3 geschlossen, Counter 3
        var adv = ProximityPairingParser.Parse(Packet(0x0E20, 0x04, 0x9A, 0x7F, 0x0B));

        Assert.NotNull(adv);
        Assert.Equal(AirPodsModel.AirPodsPro, adv.Model);
        Assert.Equal(100, adv.RightBattery);
        Assert.Equal(90, adv.LeftBattery);
        Assert.Null(adv.CaseBattery);
        Assert.True(adv.RightCharging);
        Assert.True(adv.LeftCharging);
        Assert.True(adv.CaseCharging);
        Assert.True(adv.BothInCase);
        Assert.False(adv.LeftInEar);
        Assert.False(adv.RightInEar);
        Assert.False(adv.LidOpen);
        Assert.Equal(3, adv.LidOpenCounter);
    }

    [Fact]
    public void Parses_LidOpen_WithCounter()
    {
        var adv = ProximityPairingParser.Parse(Packet(0x1420, 0x24, 0x55, 0x08, 0x05));
        Assert.NotNull(adv);
        Assert.True(adv.LidOpen);
        Assert.Equal(5, adv.LidOpenCounter);
    }

    [Fact]
    public void Parses_OnlyRightInEar_WhenRightIsCurrent()
    {
        // curr = rechts (Bit5 aus), Bit1 curr im Ohr → rechts im Ohr, links nicht
        var adv = ProximityPairingParser.Parse(Packet(0x1420, 0x02, 0x55, 0x05, 0x00));
        Assert.NotNull(adv);
        Assert.True(adv.RightInEar);
        Assert.False(adv.LeftInEar);
    }

    [Theory]
    [InlineData(0x0, 0)]
    [InlineData(0x9, 90)]
    [InlineData(0xA, 100)]
    [InlineData(0xC, 100)]
    public void BatteryNibble_MapsToPercent(int nibble, int expected)
    {
        var battery = (byte)((nibble << 4) | nibble);
        var adv = ProximityPairingParser.Parse(Packet(0x1420, 0x20, battery, (byte)nibble, 0x00));
        Assert.NotNull(adv);
        Assert.Equal(expected, adv.LeftBattery);
        Assert.Equal(expected, adv.RightBattery);
        Assert.Equal(expected, adv.CaseBattery);
    }

    [Fact]
    public void UnknownModel_StillParses()
    {
        var adv = ProximityPairingParser.Parse(Packet(0x9999, 0x2A, 0x78, 0x05, 0x00));
        Assert.NotNull(adv);
        Assert.Equal(AirPodsModel.Unknown, adv.Model);
        Assert.Equal(0x9999, adv.ModelId);
        Assert.Equal(80, adv.LeftBattery);
    }

    [Fact]
    public void WrongType_ReturnsNull()
    {
        var p = Packet(0x1420, 0x2A, 0x78, 0x05, 0x00);
        p[0] = 0x10; // Nearby-Info statt Proximity Pairing
        Assert.Null(ProximityPairingParser.Parse(p));
    }

    [Fact]
    public void TooShort_ReturnsNull()
    {
        Assert.Null(ProximityPairingParser.Parse(Packet(0x1420, 0x2A, 0x78, 0x05, 0x00).AsSpan(0, 8)));
        Assert.Null(ProximityPairingParser.Parse(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void PairingModeShortPacket_ReturnsNull()
    {
        // Echte Aufzeichnung: Länge 0x0F, Byte 2 = 0x00 → andere Struktur, wird verworfen
        var bytes = Convert.FromHexString("070F001320CC68E0382F72F1E4E4157100");
        Assert.Null(ProximityPairingParser.Parse(bytes));
    }

    [Fact]
    public void Prefix00_LongPacket_ReturnsNull()
    {
        var p = Packet(0x1420, 0x2A, 0x78, 0x05, 0x00);
        p[2] = 0x00;
        Assert.Null(ProximityPairingParser.Parse(p));
    }
}
