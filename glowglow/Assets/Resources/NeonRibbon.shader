Shader "GlowGlow/NeonRibbon"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "BarrageVisibility.cginc"
            float _GlowGlowClassic;
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o; }
            half4 frag(Output i):SV_Target
            {
                i.color.a *= OwnBarrageOpacity();
                float across=abs(i.uv.y*2-1);
                if (_GlowGlowClassic > .5)
                {
                    float alpha=i.color.a*.7258*(1-smoothstep(.6,.67,across));
                    return half4(i.color.rgb*alpha,alpha);
                }
                float filament=1-smoothstep(.04,.28,across);
                float edge=1-smoothstep(.35,1,across);
                half3 spectrum=lerp(half3(.15,.9,1),half3(1,.2,.75),i.uv.y);
                half3 color=lerp(lerp(i.color.rgb,spectrum,.5),half3(1,1,1),filament*.65);
                return half4(color*edge*i.color.a,i.color.a*edge);
            }
            ENDCG
        }
    }
}
