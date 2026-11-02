Shader "GlowGlow/NeonCore"
{
    Properties
    {
        [PerRendererData] _MainTex ("Prepared sprite", 2D) = "white" {}
        _Style ("Style", Float) = 0
        _ClipRect ("World bounds", Vector) = (-1000,-1000,1000,1000)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "BarrageVisibility.cginc"
            sampler2D _MainTex;
            float _Style;
            float _GlowGlowClassic;
            float4 _ClipRect;
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
            Output vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv;
                o.world=mul(unity_ObjectToWorld,v.vertex).xy; return o;
            }
            half4 frag(Output i):SV_Target
            {
                i.color.a *= OwnBarrageOpacity();
                clip(i.world-_ClipRect.xy); clip(_ClipRect.zw-i.world);
                float2 p=i.uv*2-1;
                float r=length(p);
                float aa=max(fwidth(r),.012);
                half alpha=tex2D(_MainTex,i.uv).a*i.color.a;
                half3 tint=i.color.rgb;
                if (_GlowGlowClassic > .5) return half4(tint*alpha,alpha);
                half3 rgb;
                if (_Style < .5) // Small projectiles: solid readable silhouette, hot core.
                {
                    float core=1-smoothstep(.12,.75,r);
                    float rim=smoothstep(.7,.88,r)*(1-smoothstep(.94,1,r));
                    rgb=lerp(tint*.75,half3(1,1,1),core*.92)+tint*rim*.3;
                }
                else if (_Style < 1.5) // Prism facets share the original circular hit silhouette.
                {
                    float facet=saturate(p.x*.65-p.y*.35+.5);
                    half3 spectrum=lerp(half3(.2,1,1),half3(.95,.3,1),facet);
                    float core=1-smoothstep(.05,.72,abs(p.x)+abs(p.y));
                    rgb=lerp(lerp(tint,spectrum,.55),half3(1,1,1),core*.95);
                    rgb+=.16*(1-smoothstep(0,.06,abs(p.x+p.y*.45)));
                }
                else if (_Style < 2.5) // Tank: dark glass and illuminated rim.
                {
                    float edge=smoothstep(.74,.82,r)*(1-smoothstep(.93,1,r));
                    float bevel=saturate(-p.x*.4+p.y*.55+.2);
                    float circuit=(1-smoothstep(.015,.035,abs(r-.63)))*.3;
                    rgb=tint*(.13+.2*bevel)+tint*(edge*.95+circuit);
                }
                else if (_Style < 3.5) // Explosion: luminous perimeter and translucent interior.
                {
                    float edge=1-smoothstep(.035,.12,abs(r-.82));
                    rgb=lerp(tint,half3(1,1,1),edge*.8);
                    alpha*=.22+.78*edge;
                }
                else // Telegraph preserves a filled warning area and a crisp dashed rim.
                {
                    float edge=1-smoothstep(.025,.08,abs(r-.9));
                    float dash=step(.38,frac((p.x+p.y)*8));
                    rgb=tint*(.8+.2*dash); alpha*=.5+.5*edge;
                }
                return half4(rgb*alpha,alpha);
            }
            ENDCG
        }
    }
}
