Shader "GlowGlow/SolarBody"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Sun ("Sun", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
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
            float _Sun;
            float _GlowGlowClassic;
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            Output vert(Input input)
            {
                Output output;
                output.vertex=UnityObjectToClipPos(input.vertex);
                output.color=input.color;
                output.uv=input.uv;
                return output;
            }
            fixed4 frag(Output input):SV_Target
            {
                input.color.a *= OwnBarrageOpacity();
                float2 p=input.uv*2-1;
                float r=length(p);
                float z=sqrt(saturate(1-dot(p,p)));
                float light=saturate(dot(float3(p,z),normalize(float3(-.55,.65,.8))));
                float bands=.93+.07*sin(p.y*24+p.x*5);
                float3 planet=input.color.rgb*(.22+.85*light)*bands;
                planet+=pow(light,18)*.22;
                float rim=smoothstep(.68,.94,r);
                float3 sun=lerp(float3(1,.35,.025),float3(1,.88,.35),rim);
                if (_GlowGlowClassic < .5)
                {
                    float grain=.5+.5*sin(p.x*31+sin(p.y*27+_Time.y*.8))*sin(p.y*35-p.x*13-_Time.y*1.1);
                    sun*=.92+.08*grain;
                    float corona=(1-smoothstep(.03,.14,abs(r-.85)))*(.65+.35*sin(p.x*24+p.y*19+_Time.y*2));
                    sun+=float3(1,.48,.08)*corona*.32;
                }
                float alpha=tex2D(_MainTex,input.uv).a*input.color.a;
                return float4(lerp(planet,sun,_Sun)*alpha,alpha);
            }
            ENDCG
        }
    }
}
