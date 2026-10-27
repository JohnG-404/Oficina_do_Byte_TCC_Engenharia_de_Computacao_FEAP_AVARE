// Holograma -> matéria (fase Aplicacao_Fisica). Usado por Materializacao.cs.
//
// _Materia = 0: holograma (linhas azuis com vãos transparentes, sem
//   transparência parcial: tiles sobrepostos não fazem faixas).
// _Materia = 1: sprite original.
// Entre os dois: a matéria "desce" do topo até a base, com uma faixa de luz
//   na linha de corte.
// _Amassar: achata o desenho na base (só visual, no vertex shader).
// _Brilho: realce do holograma (prévia ao arrastar um Collider2D).
Shader "Aplicacao/Materializacao"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _CorHolograma ("Cor clara do holograma", Color) = (0.42, 0.86, 1, 1)
        _CorEscura ("Cor escura do holograma", Color) = (0.06, 0.29, 0.45, 1)
        _CorFaixa ("Cor da faixa de materialização", Color) = (0.9, 0.98, 1, 1)
        _LinhasPorUnidade ("Linhas por unidade", Float) = 10
        _FracaoLinha ("Parte cheia de cada linha", Range(0.2, 1)) = 0.6
        _Materia ("Matéria (0 holograma, 1 sólido)", Range(0, 1)) = 0
        _TopoY ("Topo do objeto (mundo)", Float) = 1
        _BaseY ("Base do objeto (mundo)", Float) = 0
        _CentroX ("Centro X do objeto (mundo)", Float) = 0
        _Amassar ("Amassar", Float) = 0
        _Brilho ("Realce do holograma", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _CorHolograma;
                half4 _CorEscura;
                half4 _CorFaixa;
                float _LinhasPorUnidade;
                float _FracaoLinha;
                float _Materia;
                float _TopoY;
                float _BaseY;
                float _CentroX;
                float _Amassar;
                float _Brilho;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS);

                // Achata em direção à base e alarga um pouco (ou o contrário,
                // com _Amassar negativo): o "peso" do Rigidbody2D.
                ws.y = _BaseY + (ws.y - _BaseY) * (1.0 - _Amassar);
                ws.x = _CentroX + (ws.x - _CentroX) * (1.0 + _Amassar * 0.6);

                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                float y = i.positionWS.y;

                // Acima da linha de corte já é matéria.
                float corte = lerp(_TopoY + 0.2, _BaseY - 0.2, _Materia);
                float solido = step(corte, y);

                // Holograma: o desenho em dois tons de azul, em linhas
                // horizontais que sobem devagar, com um tremor leve.
                half lum = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                half3 holo = lerp(_CorEscura.rgb, _CorHolograma.rgb, saturate(lum * 1.35));
                holo = lerp(holo, _CorHolograma.rgb, _Brilho);
                float fase = frac(y * _LinhasPorUnidade - _Time.y * 0.35);
                float cheia = step(fase, saturate(_FracaoLinha + _Brilho * 0.3));
                float tremor = 0.94 + 0.06 * sin(_Time.y * 17.0 + y * 2.0);
                half4 h = half4(holo * tremor, tex.a * cheia);

                half4 c = lerp(h, tex, solido);

                // Faixa de luz onde a matéria está chegando.
                float emTransicao = step(0.001, _Materia) * step(_Materia, 0.999);
                float faixa = saturate(1.0 - abs(y - corte) / 0.14) * emTransicao * tex.a;
                c.rgb = lerp(c.rgb, _CorFaixa.rgb, faixa);
                c.a = max(c.a, faixa);
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
