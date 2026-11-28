Shader "AfterHours/BrineSilhouette"
{
    Properties { _MainTex ("Wet coat",2D)="white" {} _Cutoff ("Silhouette cutoff",Range(0,1))=.36 _Phase ("Step",Float)=0 _Coat ("Fabric",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff addshadow
        sampler2D _MainTex;
        half _Phase;
        fixed4 _Coat;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutput o)
        {
            float2 uv=IN.uv_MainTex;
            float gait=sin(_Time.y*5+_Phase);
            float boots=saturate((.53-uv.y)*3);
            uv.y+=gait*clamp((uv.x-.5)*7,-1,1)*boots*.019;
            uv.x+=gait*boots*.008;
            fixed4 coat=tex2D(_MainTex,uv);
            coat.rgb*=lerp(float3(1,1,1),_Coat.rgb,smoothstep(.14,.36,uv.y)*(1-smoothstep(.72,.88,uv.y)));
            o.Albedo=coat.rgb;
            o.Emission=coat.rgb*.33;
            o.Alpha=coat.a;
        }
        ENDCG
    }
    Fallback Off
}
