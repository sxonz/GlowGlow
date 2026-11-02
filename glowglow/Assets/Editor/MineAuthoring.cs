using UnityEditor;

public static class MineAuthoring
{
    [MenuItem("GlowGlow/Bake Mine Icon")]
    public static void BakeIcon() => WeaponIconAuthoring.Bake();
}
