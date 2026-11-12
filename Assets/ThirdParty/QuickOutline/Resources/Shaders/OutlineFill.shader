//
//  OutlineFill.shader
//  QuickOutline
//
//  Created by Chris Nolet on 2/21/18.
//  Copyright © 2018 Chris Nolet. All rights reserved.
//
//  Don't Bet the Farm changes, each marked "DBTF": the outline is pushed out a fixed number of pixels in screen
//  space, each pixel is coloured once, and "hidden" means hidden by more than _HiddenBias world units.
//

Shader "Custom/Outline Fill" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0

    _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 10)) = 2
    _HiddenBias("Hidden Bias", Float) = 0.3
  }

  SubShader {
    Tags {
      "Queue" = "Transparent+110"
      "RenderType" = "Transparent"
      "DisableBatching" = "True"
    }

    Pass {
      Name "Fill"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha
      ColorMask RGB

      Stencil {
        Ref 1
        Comp NotEqual
        Pass Replace // DBTF: overlapping parts don't blend twice, so a see-through outline stays one even shade
      }

      CGPROGRAM
      #include "UnityCG.cginc"

      #pragma vertex vert
      #pragma fragment frag

      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float3 smoothNormal : TEXCOORD3;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 position : SV_POSITION;
        fixed4 color : COLOR;
        UNITY_VERTEX_OUTPUT_STEREO
      };

      uniform fixed4 _OutlineColor;
      uniform float _OutlineWidth;
      uniform float _HiddenBias;

      v2f vert(appdata input) {
        v2f output;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 normal = any(input.smoothNormal) ? input.smoothNormal : input.normal;
        float3 viewPosition = UnityObjectToViewPos(input.vertex);
        float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, normal);

        // DBTF: the original pushed each vertex out along its 3D normal by a share of its depth, so the line was
        // thin wherever the normal leaned toward the camera and lumpy on low-poly parts. This pushes it out
        // _OutlineWidth pixels across the screen instead, the same width everywhere (ortho or perspective).
        // The vertex is also drawn _HiddenBias closer, so ground just in front of a creature's feet doesn't
        // count as hiding it.
        output.position = UnityViewToClipPos(viewPosition + float3(0, 0, _HiddenBias));
        float2 pixels = mul((float2x2)UNITY_MATRIX_P, viewNormal.xy) * _ScreenParams.xy;
        float length2 = dot(pixels, pixels);
        if (length2 > 1e-8) {
          output.position.xy += pixels * rsqrt(length2) * _OutlineWidth * 2.0 / _ScreenParams.xy * output.position.w;
        }
        output.color = _OutlineColor;

        return output;
      }

      fixed4 frag(v2f input) : SV_Target {
        return input.color;
      }
      ENDCG
    }
  }
}
