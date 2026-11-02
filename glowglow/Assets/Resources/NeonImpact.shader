Shader "GlowGlow/NeonImpact"
{
    Properties
    {
        [PerRendererData] _MainTex ("Prepared sprite", 2D) = "white" {}
        _Ring ("Ring", Float) = 0
        _ClipRect ("World bounds", Vector) = (-1000,-1000,1000,1000)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "BarrageVisibility.cginc"
            sampler2D _MainTex; float _Ring; float4 _ClipRect;
            struct Input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; o.world=mul(unity_ObjectToWorld,v.vertex).xy; return o; }
            half4 frag(Output i):SV_Target
            {
                i.color.a *= OwnBarrageOpacity();
                clip(i.world-_ClipRect.xy); clip(_ClipRect.zw-i.world);
                float2 p=i.uv*2-1;
                float r=length(p);
                float aa=max(fwidth(r),.009);
                float ring=1-smoothstep(.012,.012+aa*2,abs(r-.88));
                // Analytic shading on prepared sprite geometry avoids texture-binding flashes on first render.
                float glow=exp2(-6*dot(p,p))*(1-smoothstep(.8,1,r));
                float shard=1-smoothstep(.7,1,abs(p.x)+abs(p.y));
                float alpha=(_Ring < .5 ? glow : _Ring < 1.5 ? ring : shard)*i.color.a;
                return half4(i.color.rgb*alpha,alpha);
            }
            ENDCG
        }
    }
}
