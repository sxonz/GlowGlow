Shader "GlowGlow/NeonArena"
{
    Properties { [PerRendererData] _MainTex ("Prepared square", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend One Zero
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            half4 frag(Output i):SV_Target
            {
                float2 p=(i.uv-.5)*float2(17.4,9.3);
                float2 edge=float2(8.7,4.65)-abs(p);
                float border=min(edge.x,edge.y);
                float center=saturate(1-length(p/float2(11,7)));
                half3 color=lerp(half3(.003,.002,.009),half3(.009,.005,.021),center);
                float2 grid=abs(frac(p*.5-.5)-.5)/max(fwidth(p*.5),.0001);
                float gridLine=1-saturate(min(grid.x,grid.y));
                float2 fine=abs(frac(p*2-.5)-.5)/max(fwidth(p*2),.0001);
                float fineLine=1-saturate(min(fine.x,fine.y));
                color+=half3(.032,.021,.075)*gridLine*.18+half3(.022,.018,.048)*fineLine*.09;
                half3 side=lerp(half3(1,.15,.65),half3(.18,.72,1),smoothstep(-7,7,p.x));
                float rim=(1-smoothstep(.015,.055,border));
                float halo=exp2(-max(0,border)*11)*.13;
                float flow=pow(saturate(.5+.5*sin(p.x*.6+p.y*.9-_Time.y*1.2)),10);
                color+=side*(rim*(.5+.5*flow)+halo);
                float inset=1-smoothstep(.006,.024,abs(border-.16));
                color+=side*inset*.15;
                float cornerH=(1-smoothstep(.007,.02,abs(abs(p.y)-4.32)))*step(7.95,abs(p.x))*step(abs(p.x),8.35);
                float cornerV=(1-smoothstep(.007,.02,abs(abs(p.x)-8.35)))*step(4.02,abs(p.y))*step(abs(p.y),4.32);
                color+=side*(cornerH+cornerV)*.4;
                float radial=length(p);
                float circle=1-smoothstep(.009,.022,abs(radial-1.4));
                float cross=(1-smoothstep(.008,.022,min(abs(p.x),abs(p.y))))*(1-smoothstep(.16,.23,radial));
                color+=half3(.06,.08,.16)*(circle*.2+cross*.4);
                return half4(color,1);
            }
            ENDCG
        }
    }
}
