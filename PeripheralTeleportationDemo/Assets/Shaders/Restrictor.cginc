// For FoV calculation

#define CLIP_FAR 1
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN)	
	#define CLIP_SCREEN CLIP_FAR
#else
	#define CLIP_SCREEN 1-UNITY_NEAR_CLIP_VALUE
#endif

// Requires UnityCG.cginc
#define PI 3.1415926538

float4x4 _EyeProjection[2];
// both are in tangent
float _RestrictorRadius; 
float _RestrictorShift; // Side restrictor
float _RestrictorTranstion; // transit between foveat and periphery
float _RestrictorXScale; // Oval restrictor
float _SqlrPow; // Rounded square restrictor
float _ReverseY; // 0 in the editor, 1 in build

inline float4 screenCoords(float2 uv){
	float2 c = (uv - 0.5) * 2;
	float4 vPos = mul(_EyeProjection[unity_StereoEyeIndex], float4(c, CLIP_SCREEN, 1));
	vPos.xyz /= vPos.w;
	return vPos;
}

inline float4 screenCoords(float2 uv, int eyeIndex){
	float2 c = (uv - 0.5) * 2;
	float4 vPos = mul(_EyeProjection[eyeIndex], float4(c, CLIP_SCREEN, 1));
	vPos.xyz /= vPos.w;
	return vPos;
}

float uv2Radius(float2 uv) {
	// Calculate field of view
	float4 coords = screenCoords(uv);
	float2 adjCoords = coords.xy / (_ScreenParams.xy/2) - float2(_RestrictorShift, 0);
	bool isShift = _RestrictorShift != 0;
	adjCoords.x *= (1 + _RestrictorXScale * isShift);
	return pow(pow(abs(adjCoords.x), _SqlrPow) + pow(abs(adjCoords.y), _SqlrPow), 1 / _SqlrPow);
}


float uv2Radius2(float2 uv, int eyeIndex) {
	// Calculate field of view
	float4 coords = screenCoords(uv, eyeIndex);
	float2 adjCoords = coords.xy / (_ScreenParams.xy/2) - float2(_RestrictorShift, 0);
	bool isShift = _RestrictorShift != 0;
	adjCoords.x *= (1 + _RestrictorXScale * isShift);
	return pow(pow(abs(adjCoords.x), _SqlrPow) + pow(abs(adjCoords.y), _SqlrPow), 1 / _SqlrPow);
}


float uv2Radius3(float2 uv, int eyeIndex) {
	// Calculate field of view
	float4 coords = screenCoords(uv, eyeIndex);
	float2 adjCoords; 
	if (coords.x * _RestrictorShift > 0) {
		coords.x = 0;
	}
	adjCoords = coords.xy / (_ScreenParams.xy/2);
	return pow(pow(abs(adjCoords.x), _SqlrPow) + pow(abs(adjCoords.y), _SqlrPow), 1 / _SqlrPow);
}
