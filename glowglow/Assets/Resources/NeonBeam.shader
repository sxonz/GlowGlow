Shader "GlowGlow/NeonBeam"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ClipRect ("World bounds", Vector) = (-1000,-1000,1000,1000)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "BarrageVisibility.cginc"
            sampler2D _MainTex;
            float4 _ClipRect;
            float _GlowGlowClassic;
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
                input.color.a *= OwnBarrageOpacity();
                clip(input.world-_ClipRect.xy);
                clip(_ClipRect.zw-input.world);
                float across=abs(input.uv.y*2-1);
                float core=1-smoothstep(.08,.3,across);
                float body=1-smoothstep(.36,1,across);
                float muzzle=(1-smoothstep(0,.045,input.uv.x))*(1-across);
                float flow=.96+.04*sin(input.uv.x*32-_Time.y*24);
                float filament=0;
                if (_GlowGlowClassic < .5)
                {
                    float arc=abs(input.uv.y-.5-.23*sin(input.uv.x*51-_Time.y*31)*sin(input.uv.x*17+_Time.y*13));
                    filament=(1-smoothstep(.008,.027,arc))*.24;
                }
                fixed4 color;
                color.rgb=lerp(input.color.rgb, fixed3(1,1,1), saturate(core*.88+muzzle*.35+filament));
                // Keep a visible colored boundary throughout the damaging lifetime.
                color.a=(.28+.72*body)*flow*input.color.a*tex2D(_MainTex,input.uv).a;
                color.rgb*=color.a;
                return color;
            }
            ENDCG
        }
    }
}
