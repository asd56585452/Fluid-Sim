Shader "Instanced/Particle3DSurfUnlit" {
    Properties{
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Alpha("Alpha", Range(0,1)) = 0.5
    }
    SubShader{
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            sampler2D _MainTex;
            float _Alpha;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<float3> Positions;
                StructuredBuffer<float3> Velocities;
                StructuredBuffer<float2> Densitys;
            #endif

            SamplerState linear_clamp_sampler;
            float velocityMax;

            float scale;
            float3 colour;

            uint mask; //mask

            void setup()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    float3 pos = Positions[unity_InstanceID];

                    float s = scale * (unity_InstanceID < mask);

                    unity_ObjectToWorld._11_21_31_41 = float4(s, 0, 0, 0);
                    unity_ObjectToWorld._12_22_32_42 = float4(0, s, 0, 0);
                    unity_ObjectToWorld._13_23_33_43 = float4(0, 0, s, 0);
                    unity_ObjectToWorld._14_24_34_44 = float4(pos, 1);
                    unity_WorldToObject = unity_ObjectToWorld;
                    unity_WorldToObject._14_24_34 *= -1;
                    unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
                #endif
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                setup();

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    float speed = length(Velocities[unity_InstanceID]);
                    float speedT = saturate(speed / velocityMax);
                    float colT = speedT;
                    o.color = float4(colour.x, colour.y, colour.z, _Alpha);
                #else
                    o.color = float4(1, 1, 1, _Alpha);
                #endif

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return i.color;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
