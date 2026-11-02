/// <summary>Initial 100-HP tuning. Secondary attacks have independent damage values.</summary>
public static class BarrageDamage
{
    public const int Basic = 6;
    public const int SpreadInner = 8, SpreadOuter = 6;
    public const int BombFragment = 6;
    public const int PulseCollision = 18;
    public const int MeteorBlast = 20, MeteorFragment = 4;
    public const int MiniMeteor = 16, MiniMeteorBlast = 10, MiniMeteorFragment = 2;
    public const int OctoSplit = 4, PrismBlast = 8;
    public const int Planet = 12, ReleasedPlanet = 16, Comet = 8;
    public const int SatelliteBullet = 5, BlossomPetal = 6;
    public const int ConstellationTrace = 14;

    // Editor installers use the same defaults as the saved weapon assets.
    public static int DefaultFor(string name) => name switch
    {
        "Laser Shot" => 22,
        "Spread Shot" => 10,
        "Bomb" => 30,
        "Electric Pulse" => 0,
        "Overdrive" => 20,
        "Bouncer" => 10,
        "Orbit Orb" => 16,
        "4-axis" => 10,
        "Meteor Dive" => 32,
        "Octo Shot" => 8,
        "Boomerang" => 24,
        "Prism Shot" => 10,
        "Solar System" => 24,
        "Mine" => 32,
        "Firefly Convergence" => 8,
        "Twin Satellites" => 12,
        "Blossom Burst" => 0,
        "Constellation Ignition" => 24,
        _ => Basic
    };
}
