// Upgrade NOTE: commented out 'float4x4 _WorldToCamera', a built-in variable
// Upgrade NOTE: replaced '_WorldToCamera' with 'unity_WorldToCamera'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Copyright (c) 2017 Jakub Boksansky, Adam Pospisil - All Rights Reserved
// Wilberforce Colorbleed Unity Plugin 1.0

Shader "Hidden/Wilberforce/Colorbleed"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
	CGINCLUDE
	
		#pragma target 3.0
		
		#include "UnityCG.cginc"

		sampler2D _CameraDepthNormalsTexture;
		float4 _CameraDepthNormalsTexture_ST;
		sampler2D _CameraGBufferTexture2;
		sampler2D _CameraDepthTexture;
		uniform int useGBuffer;

		sampler2D doomTexture;
		uniform float4x4 invProjMatrix;
		uniform float4x4 projMatrix;
		uniform float radius;
		uniform float radiusRcp;
		uniform int _length;
		uniform int _lengthOpenGLBug;
		uniform float bigweight;
		uniform float bigweightEight;
		uniform float giPow;
		uniform int giBackfaces;
		uniform int doomMode;
		uniform float2 _tsizedoom;
		uniform int adaptive;
		uniform float adaptiveMin;
		uniform float adaptiveMax;
		uniform float distanceAttPower;
		uniform int distanceAtt;
		uniform int cosineAtt;
		
		#if UNITY_VERSION < 540
		     // float4x4 _WorldToCamera;
		#endif

		sampler2D textureCB;
		float4 _colorTint;
		uniform int forceFlip;
		uniform int adaptiveMode;

		uniform float4 _samp[70];
		uniform float4 samp8[70];
		
		float4 _ProjInfo;

		sampler2D _MainTex;
		float4 _MainTex_TexelSize;
		float4 _MainTex_ST;

		uniform float2 _tsize;
		uniform float _tresh;
		uniform	int giBlur;
			
		uniform float LumaTreshold;
		uniform float LumaKneeWidth;
		uniform float LumaTwiceKneeWidthRcp;
		uniform float LumaKneeLinearity;
		uniform int IsLumaSensitive;
		uniform int LumaMode;
		uniform float _giPresence;
		uniform float giDarkness;
		
		uniform int enhancedBlurSize;
		uniform float4 gauss[99];
		uniform float gaussWeight;
		uniform int _raymarchSteps;
		uniform float backfacesRadiusMultiplier;
		uniform float _saturation;

		static const float adaptiveLengths[16] = { 32, 16, 16, 8, 8, 8, 8, 4, 4, 4, 4, 4, 4, 4, 4, 2 };
		static const float adaptiveStarts[16] = { 0, 32, 32, 48, 48, 48, 48, 56, 56, 56, 56, 56, 56, 56, 56, 60 };
		static const float adaptiveWeights[16] = { 0.049036f, 0.0942773f, 0.0942773f, 0.2079888f, 0.2079888f, 0.2079888f, 0.2079888f, 0.38241502f, 0.38241502f, 0.38241502f, 0.38241502f, 0.38241502f, 0.38241502f, 0.38241502f, 0.38241502f, 0.76038688f };
		static const float2 noiseSamples[9] = { float2(1.0f, 0.0f), float2(-0.939692f, 0.342022f), float2(0.173644f, -0.984808f), float2(0.173649f, 0.984808f), float2(-0.500003f, -0.866024f), float2(0.766045f, 0.642787f), float2(-0.939694f, -0.342017f), float2(0.766042f, -0.642791f), float2(-0.499999f, 0.866026f),};
		static const float3 randomness[9] = { float3(0.7071068,0,0.7071068),float3(0.5416752,0.4545195,0.7071068),float3(0.1227878,0.6963642,0.7071068),float3(-0.3535534,0.6123725,0.7071068),float3(-0.664463,0.2418448,0.7071068),float3(-0.664463,-0.2418448,0.7071068),float3(-0.3535533,-0.6123725,0.7071068),float3(0.1227878,-0.6963642,0.7071068),float3(0.5416752,-0.4545196,0.7071068),};

		struct v2fShed {
			float4 pos : SV_POSITION;
			#ifdef UNITY_SINGLE_PASS_STEREO
			float4 shed2 : TEXCOORD2;
			#endif
			float4 shed : TEXCOORD1;
			float2 uv : TEXCOORD0;
		};
		
		struct v2fSingle {
			float4 pos : SV_POSITION;
			float2 uv : TEXCOORD0;
		};

		struct v2fDouble {
			float4 pos : SV_POSITION;
			float2 uv[2] : TEXCOORD0;
		};

		v2fShed vertShed(appdata_img v)
		{
			v2fShed o;
			#if UNITY_VERSION >= 540
			o.pos = UnityObjectToClipPos(v.vertex);
			#else
			o.pos = UnityObjectToClipPos(v.vertex);
			#endif
			#ifdef UNITY_SINGLE_PASS_STEREO
				o.uv = UnityStereoScreenSpaceUVAdjust(v.texcoord, _CameraDepthNormalsTexture_ST);
			#else
				o.uv = TRANSFORM_TEX(v.texcoord, _CameraDepthNormalsTexture);
			#endif

			#if UNITY_UV_STARTS_AT_TOP
			if (_MainTex_TexelSize.y < 0)
				o.uv.y = 1.0f - o.uv.y;
			#endif
				
			#ifdef UNITY_SINGLE_PASS_STEREO
				float2 tempUV1 = float2(o.uv.x * 2.0f, o.uv.y);
				float2 tempUV2 = float2(o.uv.x * 2.0f - 1.0f, o.uv.y);
				o.shed = mul(invProjMatrix, float4(tempUV1 * 2.0f - 1.0f, 1.0f, 1.0f));
				o.shed /= o.shed.w;
				o.shed2 = mul(invProjMatrix, float4(tempUV2 * 2.0f - 1.0f, 1.0f, 1.0f));
				o.shed2 /= o.shed2.w;
			#else
				o.shed = mul(invProjMatrix, float4(o.uv* 2.0f - 1.0f, 1.0f, 1.0f));
				o.shed /= o.shed.w;
			#endif

			return o;
		}
			
		v2fSingle vertSingle(appdata_img v)
		{
			v2fSingle o;
			#if UNITY_VERSION >= 540
			o.pos = UnityObjectToClipPos(v.vertex);
			#else
			o.pos = UnityObjectToClipPos(v.vertex);
			#endif

			#ifdef UNITY_SINGLE_PASS_STEREO
				o.uv = UnityStereoScreenSpaceUVAdjust(v.texcoord, _MainTex_ST);
			#else
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
			#endif
			return o;
		}

		v2fDouble vertDouble(appdata_img v)
		{
			v2fDouble o;
			#if UNITY_VERSION >= 540
			o.pos = UnityObjectToClipPos(v.vertex);
			#else
			o.pos = UnityObjectToClipPos(v.vertex);
			#endif

			#ifdef UNITY_SINGLE_PASS_STEREO
			float2 temp = UnityStereoScreenSpaceUVAdjust(v.texcoord, _MainTex_ST);
			#else
			float2 temp = TRANSFORM_TEX(v.texcoord, _MainTex);
			#endif
			o.uv[0] = temp;
			o.uv[1] = temp;
				
			#if UNITY_UV_STARTS_AT_TOP
			if (_MainTex_TexelSize.y < 0)
				o.uv[1].y = 1.0f - o.uv[1].y;
			#endif

			#ifndef SHADER_API_GLCORE
			#ifndef SHADER_API_OPENGL
			#ifndef SHADER_API_GLES
			#ifndef SHADER_API_GLES3
			if (forceFlip != 0) o.uv[0].y = 1.0f - o.uv[0].y;
			#endif
			#endif
			#endif
			#endif

			return o;
		}

		float3 getShed(float2 uv, float2 iuv){
			float4 sampShed;
			#ifdef UNITY_SINGLE_PASS_STEREO
				float2 tempUV;
				if (iuv.x > .5f) {
					tempUV = float2(uv.x * 2.0f - 1.0f, uv.y);
				} else {
					tempUV = float2(uv.x * 2.0f, uv.y);
				}
				sampShed = mul(invProjMatrix, float4(tempUV * 2.0f - 1.0f, 1.0f, 1.0f));
			#else
				sampShed = mul(invProjMatrix, float4(uv.xy * 2.0f - 1.0f, 1.0f, 1.0f));
			#endif
			sampShed /= sampShed.w;
			return sampShed.xyz;
		}

		float luma(half4 color) {
			return 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
		}

		fixed4 bleedRaymarch(v2fShed i, int isDoomMode, int isAdaptive, int UseGBuffer, float power, float4 kernel[70], float kernelWeight, int kernelLength, int raymarchSteps, int randomizeLengths)
		{
			float4 doomness = 0.0f;
			if (isDoomMode != 0) {
				doomness += tex2Dlod(doomTexture, float4(i.uv + float2(_tsizedoom.x, _tsizedoom.y), 0, 0)).rgba;
				doomness += tex2Dlod(doomTexture, float4(i.uv + float2(-_tsizedoom.x, _tsizedoom.y), 0, 0)).rgba;
				doomness += tex2Dlod(doomTexture, float4(i.uv + float2(-_tsizedoom.x, -_tsizedoom.y), 0, 0)).rgba;
				doomness += tex2Dlod(doomTexture, float4(i.uv + float2(_tsizedoom.x, -_tsizedoom.y), 0, 0)).rgba;
				if (isDoomMode == 1 && doomness.r > 3.8f && doomness.g > 3.8f && doomness.b > 3.8f) return float4(1.0f, 1.0f, 1.0f, 1.0f);
			}

			float _z = 0.0f;
			float3 pixelNormal;
			float3 sampleNormal;
				
			if (UseGBuffer == 0) {
				DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, i.uv), _z, pixelNormal);
			} else {
				_z = Linear01Depth(tex2D(_CameraDepthTexture, i.uv).r);

				#if UNITY_VERSION >= 540
				pixelNormal = mul((float3x3)unity_WorldToCamera, tex2D(_CameraGBufferTexture2, i.uv).xyz * 2.0f - 1.0f);
				#else 
				pixelNormal = mul((float3x3)unity_WorldToCamera, tex2D(_CameraGBufferTexture2, i.uv).xyz * 2.0f - 1.0f);
				#endif

				pixelNormal.z = -pixelNormal.z;
			}

			if (_z > 0.99f) return 1.0f; 
			int noiseIdx = dot(int2(fmod(i.uv.xy * _MainTex_TexelSize.zw, 3)), int2(1, 3));
			float2 _dit = noiseSamples[noiseIdx];

			float3 pixelViewSpacePosition = (i.shed.rgb * _z);
	
			#ifdef UNITY_SINGLE_PASS_STEREO
			if (i.uv.x > .5f) {
				pixelViewSpacePosition = (i.shed2.rgb * _z);
			}
			#endif

			int start = 0;

			if (isAdaptive != 0) {
				if (isDoomMode == 2 && (doomness.r > 3.8f && doomness.g > 3.8f && doomness.b > 3.8f)) {
					start = 48;
					kernelLength = 8;
				} else {
					int adaptiveLevel = (int) (max(0.0f, min((((pixelViewSpacePosition.z - adaptiveMin) / (adaptiveMax - adaptiveMin)) ) * 15.0f, 15.0f)));
					kernelLength = adaptiveLengths[adaptiveLevel];
					start = adaptiveStarts[adaptiveLevel];
				}
			} else if (isDoomMode == 2 && (doomness.r > 3.8f && doomness.g > 3.8f && doomness.b > 3.8f)) {
				start = _lengthOpenGLBug;
				kernelLength = 4;
			}
				
			float3x3 _rm3 = float3x3(_dit.x, _dit.y, 0, -_dit.y, _dit.x, 0, 0, 0, 1);
			float3 gi = float3(0.0f, 0.0f, 0.0f);

			float3 rvec = float3(0.22646, 0.56614, -0.79259); 
			float3 tangent = normalize(rvec - cross(dot(rvec, pixelNormal), pixelNormal));
			float3 bitangent = cross(tangent, pixelNormal);
			float3x3 tbn = float3x3(tangent, bitangent, pixelNormal);
			float sd = _z;

			_rm3 = mul(_rm3, tbn);

			float3 shedA = getShed(float2(0.0f,0.0f), i.uv);
			float3 shedB = getShed(float2(1.0f,1.0f), i.uv);

			int ii = 0;
			int upTo = min(16, kernelLength);
			float raymarchStepSize = 1.0f / float(raymarchSteps);
		
			for (int j = start; j < start + kernelLength; j++) {
					float3 sam = kernel[ii + j];
					if (randomizeLengths == 1) {
						sam *= (1.0f / float(kernelLength+1) * float((j - start)+1));
					}

					float3 sampleRayStep = mul(sam * radius, _rm3) * raymarchStepSize;

					float3 sampleRay = sampleRayStep/**0.5f*/;
					float rayCurrentDistance = 0.0f;

					gi += 1.0f; 

					for (int s = 0; s < raymarchSteps; s++) {
						
						float3 sampleViewSpacePosition = pixelViewSpacePosition + sampleRay;

						float4 _off = mul(projMatrix, float4(sampleViewSpacePosition, 1.0f));
						_off.xy /= _off.w;
						_off.xy = _off.xy * 0.5f + 0.5f;
					
						#ifdef UNITY_SINGLE_PASS_STEREO
						if (i.uv.x < .5f) {
							_off.x *= 0.5f;
						} else {
							_off.x = 0.5f + (_off.x * 0.5f);
						}
						#endif

						if (UseGBuffer == 0) {
							DecodeDepthNormal(tex2Dlod(_CameraDepthNormalsTexture, float4(_off.xy,0,0)), _z, sampleNormal);
						} else {
							_z = Linear01Depth(tex2Dlod(_CameraDepthTexture, float4(_off.xy,0,0)).r);
							
							#if UNITY_VERSION >= 540
							sampleNormal = mul((float3x3)unity_WorldToCamera, tex2Dlod(_CameraGBufferTexture2, float4(_off.xy,0,0)).xyz * 2.0f - 1.0f);
							#else 
							sampleNormal = mul((float3x3)unity_WorldToCamera, tex2Dlod(_CameraGBufferTexture2, float4(_off.xy,0,0)).xyz * 2.0f - 1.0f);
							#endif

							sampleNormal.z = -sampleNormal.z;
						}

						float3 sampledPointViewSpacePosition = (lerp(shedA, shedB, _off) * _z);

						if (sampleViewSpacePosition.z > sampledPointViewSpacePosition.z) {
							rayCurrentDistance += raymarchStepSize * radius;
							sampleRay += sampleRayStep;
							continue;
						}
						
						gi -= 1.0f;

						float3 samplePointDirection = sampledPointViewSpacePosition - pixelViewSpacePosition;
						float sampleDistance = length(samplePointDirection);
						if (sampleDistance > radius) { 
							gi += 1.0f;
							break;
						} else {
							
							samplePointDirection = normalize(samplePointDirection);

							float cosineOrigin = dot(samplePointDirection, sampleNormal);

							float adjustedRadius = radius;
							float distanceAttenuation = 1.0f; 

							if (giBackfaces == 1 && cosineOrigin >= 0.0f) {
								adjustedRadius =  radius * smoothstep(0, 1,backfacesRadiusMultiplier);
								
								if(randomizeLengths != 0)
									distanceAttenuation = 1.0f - min(1.0f, (radius*length(sam))/adjustedRadius); 
							}
							if (randomizeLengths == 0) distanceAttenuation = /*pow(*/(1.0f - min(1.0f, (sampleDistance / adjustedRadius)))/*, distanceAttPower)*/; 

							float cosineNormals = max(0.0f, (dot(sampleNormal, pixelNormal)));
							
							float weight = 1.0f;

							weight *= (1.0f - cosineNormals);
							weight *= distanceAttenuation;
							weight *= max(0.0f, dot(pixelNormal, samplePointDirection));
						
							if (weight < 0.1f) {  
								gi += 1.0f;
							} else {

								#if UNITY_UV_STARTS_AT_TOP
								if (_MainTex_TexelSize.y < 0)
									_off.y = 1.0f - _off.y;
								#endif

								#ifndef SHADER_API_GLCORE
								#ifndef SHADER_API_OPENGL
								#ifndef SHADER_API_GLES
								#ifndef SHADER_API_GLES3
								if (forceFlip != 0) _off.y = 1.0f - _off.y;
								#endif
								#endif
								#endif
								#endif

								float3 color = tex2Dlod(_MainTex, float4(_off.xy,0,0)).rgb;
								
								gi += lerp(float3(1, 1, 1), color, weight);
							}
							break;
						}

					}
			}

			gi = gi / float(kernelLength); 
	
			gi = pow(gi, power); 

			float2 mainUV = i.uv;
			#if UNITY_UV_STARTS_AT_TOP
			if (_MainTex_TexelSize.y < 0)
				mainUV.y = 1.0f - mainUV.y;
			#endif

			#ifndef SHADER_API_GLCORE
			#ifndef SHADER_API_OPENGL
			#ifndef SHADER_API_GLES
			#ifndef SHADER_API_GLES3
			if (forceFlip != 0) mainUV.y = 1.0f - mainUV.y;
			#endif
			#endif
			#endif
			#endif

			float4 mainColor = tex2Dlod(_MainTex, float4(mainUV,0,0)).rgba;
			return fixed4(gi, luma(mainColor));
		}

		half4 mixing(v2fDouble i, int isLumaSensitivity, int lumaMode, int isCBOnly)
		{	
			half4 color = tex2D(_MainTex, i.uv[0]);
			half3 gi = tex2D(textureCB, i.uv[1]).rgb;

			if (_saturation > 0.0f) {
				float lum = luma(float4(gi, 1));
				gi = gi + (_saturation * (lum - gi));
			}

			if (isLumaSensitivity != 0) {
				float Y;
				if (lumaMode == 1) {
				    Y = luma(color);
				} else {
					Y = max(max(color.r, color.g), color.b);
				}

				Y = (Y - (LumaTreshold - LumaKneeWidth)) * LumaTwiceKneeWidthRcp;
				float x = min(1.0f, max(0.0f, Y));
				float n = ((-pow(x, LumaKneeLinearity) + 1));
				gi = lerp(float3(1.0f, 1.0f, 1.0f), gi, n);
			}

			float sMax = max(gi.r, max(gi.g, gi.b));
			float sMin = min(gi.r, min(gi.g, gi.b));
			float sat = 0.0f;
			if (sMax > 0.01f) sat = (sMax - sMin) / sMax;
			float _satMapped = 1.0f - (sat*_giPresence);
			gi = lerp(gi, gi * (1.0f / sMax), giDarkness); //< Tatry su krute
			gi = lerp(float3(1.0f, 1.0f, 1.0f), gi, _satMapped);

			if (isCBOnly == 1) return half4(gi, 1);
			color.rgb *= gi;
			return color;
		}
		
		half4 enhancedBlur(float2 uv, float2 direction, int UseGBuffer)
		{
			#ifndef SHADER_API_GLCORE
			#ifndef SHADER_API_OPENGL
			#ifndef SHADER_API_GLES
			#ifndef SHADER_API_GLES3
			if (forceFlip != 0) uv.y = 1.0f - uv.y;
			#endif
			#endif
			#endif
			#endif
			
			float4 result = float4(0.0f, 0.0f, 0.0f, 0.0f);
			float _totgi = enhancedBlurSize * 2.0f + 1.0f;
			int idx = 0;
            float _oz;
			if (UseGBuffer == 0) {
				_oz = -DecodeFloatRG(tex2D(_CameraDepthNormalsTexture, uv).zw) * _ProjectionParams.z;
			} else {
				_oz = -Linear01Depth(tex2D(_CameraDepthTexture, uv).r) * _ProjectionParams.z;
			}
			
			float4 _ogi = tex2D(_MainTex, uv).rgba;
			float weight = gaussWeight;

			for (int i = -enhancedBlurSize; i <= enhancedBlurSize; ++i) {
				float2 offset = (direction * float(i)) * _tsize;

				float _nz;
				if (UseGBuffer == 0) {
					_nz = -DecodeFloatRG(tex2Dlod(_CameraDepthNormalsTexture, float4(uv + offset, 0, 0)).zw) * _ProjectionParams.z;
				} else {
					_nz = -Linear01Depth(tex2Dlod(_CameraDepthTexture, float4(uv + offset, 0, 0)).r) * _ProjectionParams.z;
				}

				float4 _ngi = tex2Dlod(_MainTex, float4(uv + offset, 0, 0)).rgba;
				float _nl = dot(float3(0.3f, 0.3f, 0.3f), _ngi.rgb);

				if (giBlur != 3 && abs(_ogi.a - _ngi.a) > 0.1f) {
					weight -= gauss[idx].x;
				} else {
					result.rgba += _ngi.rgba * gauss[idx].x;
				}

				idx++;
			}

			return half4(result.rgba / weight);
		}


	ENDCG
	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass{CGPROGRAM
			#pragma vertex vertShed #pragma fragment frag
			half4 frag(v2fShed i) : SV_Target { return bleedRaymarch(i, doomMode, adaptiveMode, 0, giPow, _samp, bigweight, _length, 1, 1); }
			ENDCG}
			
		Pass{CGPROGRAM
			#pragma vertex vertShed #pragma fragment frag
			half4 frag(v2fShed i) : SV_Target { return bleedRaymarch(i, 0, 0, 0, 10.0f, samp8, bigweightEight, 8, 1, 1); }
			ENDCG}

		Pass{CGPROGRAM
			#pragma vertex vertShed #pragma fragment frag
			half4 frag(v2fShed i) : SV_Target { return bleedRaymarch(i, doomMode, adaptiveMode, 1, giPow, _samp, bigweight, _length, 1, 1); }
			ENDCG}
			
		Pass{CGPROGRAM
			#pragma vertex vertShed #pragma fragment frag
			half4 frag(v2fShed i) : SV_Target { return bleedRaymarch(i, 0, 0, 1, 10.0f, samp8, bigweightEight, 8, 1, 1); }
			ENDCG}

		Pass{CGPROGRAM
			#pragma vertex vertShed #pragma fragment frag
			half4 frag(v2fShed i) : SV_Target { return bleedRaymarch(i, doomMode, adaptiveMode, 0, giPow, _samp, bigweight, _length, _raymarchSteps, 0); }
			ENDCG}

		Pass{CGPROGRAM
			#pragma vertex vertShed #pragma fragment frag
			half4 frag(v2fShed i) : SV_Target { return bleedRaymarch(i, 0, 0, 0, 10.0f, samp8, bigweightEight, 8, 3, 0); }
			ENDCG}

		Pass{CGPROGRAM
			#pragma vertex vertShed #pragma fragment frag
			half4 frag(v2fShed i) : SV_Target { return bleedRaymarch(i, doomMode, adaptiveMode, 1, giPow, _samp, bigweight, _length, _raymarchSteps, 0); }
			ENDCG}

		Pass{CGPROGRAM
			#pragma vertex vertShed #pragma fragment frag
			half4 frag(v2fShed i) : SV_Target { return bleedRaymarch(i, 0, 0, 1, 10.0f, samp8, bigweightEight, 8, 3, 0); }
			ENDCG}

		Pass{CGPROGRAM
			#pragma vertex vertSingle #pragma fragment frag
			half4 frag(v2fSingle i) : SV_Target { return enhancedBlur(i.uv, float2(1.0f, 0.0f), useGBuffer); }
			ENDCG}

		Pass{CGPROGRAM
			#pragma vertex vertSingle #pragma fragment frag
			half4 frag(v2fSingle i) : SV_Target { return enhancedBlur(i.uv, float2(0.0f, 1.0f), useGBuffer); }
			ENDCG}

		Pass{CGPROGRAM
			#pragma vertex vertDouble #pragma fragment frag
			half4 frag(v2fDouble i) : SV_Target { return mixing(i, IsLumaSensitive, LumaMode, 0); }
			ENDCG}

		Pass{CGPROGRAM
			#pragma vertex vertDouble #pragma fragment frag
			half4 frag(v2fDouble i) : SV_Target { return mixing(i, IsLumaSensitive, LumaMode, 1); }
			ENDCG}
			
	}
}
