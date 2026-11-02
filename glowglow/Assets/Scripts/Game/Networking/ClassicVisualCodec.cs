using FishNet.CodeGenerating;
using FishNet.Serializing;
using UnityEngine;

namespace GlowGlow.Online
{
    // Full, independently usable object samples. Sprite packets omit all line-only fields.
    [UseGlobalCustomSerializer]
    public struct ClassicVisual
    {
        public int Id, Sprite, Order, Layer;
        public Vector3 Position, Scale;
        public float Rotation, StartWidth, EndWidth;
        public Color Color, EndColor;
        public bool FlipX, FlipY, Loop, Clipped;
        public byte Glow, Owner;
        public Vector4 ClipRect;
        public Vector2[] Points;
    }

    public static class ClassicVisualCodec
    {
        // FishNet treats collections of a global custom type as custom too.
        public static void WriteClassicVisualArray(this Writer writer, ClassicVisual[] values)
        {
            int count = values?.Length ?? 0;
            writer.WriteUInt16((ushort)count);
            for (int i = 0; i < count; i++) writer.WriteClassicVisual(values[i]);
        }

        public static ClassicVisual[] ReadClassicVisualArray(this Reader reader)
        {
            int count = reader.ReadUInt16();
            var values = new ClassicVisual[count];
            for (int i = 0; i < count; i++) values[i] = reader.ReadClassicVisual();
            return values;
        }

        public static int SizeOf(ClassicVisual value) => value.Sprite > 0
            ? 34 + (value.Clipped ? 16 : 0) : 29 + (value.Points?.Length ?? 0) * 8;

        public static void WriteClassicVisual(this Writer writer, ClassicVisual value)
        {
            writer.WriteInt32Unpacked(value.Id);
            // Sprite IDs use six bits; ownership fits in the two previously unused bits.
            writer.WriteByte((byte)((value.Sprite & 63) | ((value.Owner & 3) << 6)));
            byte flags = (byte)((value.FlipX ? 1 : 0) | (value.FlipY ? 2 : 0) | (value.Loop ? 4 : 0)
                | (value.Clipped ? 8 : 0) | ((value.Glow & 15) << 4));
            writer.WriteByte(flags);
            writer.WriteInt16((short)value.Order);
            writer.WriteInt32Unpacked(value.Layer);
            writer.WriteColor32(value.Color);
            if (value.Sprite > 0)
            {
                writer.WriteVector2(value.Position);
                writer.WriteVector2(value.Scale);
                writer.WriteUInt16((ushort)Mathf.RoundToInt(Mathf.Repeat(value.Rotation, 360) * (65535f / 360)));
                if (value.Clipped) writer.WriteVector4(value.ClipRect);
            }
            else
            {
                writer.WriteSingle(value.StartWidth); writer.WriteSingle(value.EndWidth);
                writer.WriteColor32(value.EndColor);
                int count = value.Points?.Length ?? 0;
                writer.WriteByte((byte)count);
                for (int i = 0; i < count; i++) writer.WriteVector2(value.Points[i]);
            }
        }

        public static ClassicVisual ReadClassicVisual(this Reader reader)
        {
            int id = reader.ReadInt32Unpacked();
            byte spriteAndOwner = reader.ReadByte();
            var value = new ClassicVisual { Id = id, Sprite = spriteAndOwner & 63, Owner = (byte)(spriteAndOwner >> 6) };
            byte flags = reader.ReadByte();
            value.FlipX = (flags & 1) != 0; value.FlipY = (flags & 2) != 0;
            value.Loop = (flags & 4) != 0; value.Clipped = (flags & 8) != 0;
            value.Glow = (byte)(flags >> 4);
            value.Order = reader.ReadInt16(); value.Layer = reader.ReadInt32Unpacked();
            value.Color = reader.ReadColor32();
            if (value.Sprite > 0)
            {
                value.Position = reader.ReadVector2();
                Vector2 scale = reader.ReadVector2();
                value.Scale = new Vector3(scale.x, scale.y, 1);
                value.Rotation = reader.ReadUInt16() * (360f / 65535);
                if (value.Clipped) value.ClipRect = reader.ReadVector4();
            }
            else
            {
                value.StartWidth = reader.ReadSingle(); value.EndWidth = reader.ReadSingle();
                value.EndColor = reader.ReadColor32();
                int count = reader.ReadByte();
                value.Points = new Vector2[count];
                for (int i = 0; i < count; i++) value.Points[i] = reader.ReadVector2();
            }
            return value;
        }
    }
}

