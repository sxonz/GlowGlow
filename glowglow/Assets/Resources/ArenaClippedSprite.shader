Shader "GlowGlow/ArenaClippedSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ClipRect ("World bounds", Vector) = (-1000,-1000,1000,1000)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _ClipRect;
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
            Output vert(Input input)
            {
                Output output;
                output.vertex=UnityObjectToClipPos(input.vertex);
                output.world=mul(unity_ObjectToWorld,input.vertex).xy;
                output.color=input.color;
                output.uv=input.uv;
                return output;
            }
            fixed4 frag(Output input):SV_Target
            {
                clip(input.world-_ClipRect.xy);
                clip(_ClipRect.zw-input.world);
                fixed4 color=tex2D(_MainTex,input.uv)*input.color;
                color.rgb*=color.a;
                return color;
            }
            ENDCG
        }
    }
}
