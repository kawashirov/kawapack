#ifndef KAWAFLT_FEATURE_PENETRATION_SYSTEM_INCLUDED
#define KAWAFLT_FEATURE_PENETRATION_SYSTEM_INCLUDED

/*
	Deep Penetration System features
*/

#if defined(PENETRATION_SYSTEM_ON)
	uniform float _DPS_Toggle;
	#if defined(PENETRATION_SYSTEM_ORIFICE)
		uniform float _OrificeChannel;
		uniform sampler2D _OrificeData;
		uniform float _EntryOpenDuration;
		uniform float _Shape1Depth;
		uniform float _Shape1Duration;
		uniform float _Shape2Depth;
		uniform float _Shape2Duration;
		uniform float _Shape3Depth;
		uniform float _Shape3Duration;
		uniform float _BlendshapePower;
		uniform float _BlendshapeBadScaleFix;
	#elif defined(PENETRATION_SYSTEM_PENETRATOR)
		uniform float _SqueezeDist;
		uniform float _Length;
		uniform float _Wriggle;
		uniform float _BulgePower;
		uniform float _BulgeOffset;
		uniform float _WriggleSpeed;
		uniform float _ReCurvature;
		uniform float _Curvature;
		uniform float _squeeze;
		uniform float _EntranceStiffness;
		uniform float _OrificeChannel;
	#endif
	
	void GetBestLights( float Channel, inout int orificeType, inout float3 orificePositionTracker, inout float3 orificeNormalTracker, inout float3 penetratorPositionTracker, inout float penetratorLength ) {
	float ID = step( 0.5 , Channel );
	float baseID = ( ID * 0.02 );
	float holeID = ( baseID + 0.01 );
	float ringID = ( baseID + 0.02 );
	float normalID = ( 0.05 + ( ID * 0.01 ) );
	float penetratorID = ( 0.09 + ( ID * -0.01 ) );
	float4 orificeWorld;
	float4 orificeNormalWorld;
	float4 penetratorWorld;
	float penetratorDist=100;
	for (int i=0;i<4;i++) {
		float range = (0.005 * sqrt(1000000 - unity_4LightAtten0[i])) / sqrt(unity_4LightAtten0[i]);
		if (length(unity_LightColor[i].rgb) < 0.01) {
			if (abs(fmod(range,0.1)-holeID)<0.005) {
				orificeType=0;
				orificeWorld = float4(unity_4LightPosX0[i], unity_4LightPosY0[i], unity_4LightPosZ0[i], 1);
				orificePositionTracker = mul( unity_WorldToObject, orificeWorld ).xyz;
			}
			if (abs(fmod(range,0.1)-ringID)<0.005) {
				orificeType=1;
				orificeWorld = float4(unity_4LightPosX0[i], unity_4LightPosY0[i], unity_4LightPosZ0[i], 1);
				orificePositionTracker = mul( unity_WorldToObject, orificeWorld ).xyz;
			}
			if (abs(fmod(range,0.1)-normalID)<0.005) {
				orificeNormalWorld = float4(unity_4LightPosX0[i], unity_4LightPosY0[i], unity_4LightPosZ0[i], 1);
				orificeNormalTracker = mul( unity_WorldToObject, orificeNormalWorld ).xyz;
			}
			if (abs(fmod(range,0.1)-penetratorID)<0.005) {
				float3 tempPenetratorPositionTracker = penetratorPositionTracker;
				penetratorWorld = float4(unity_4LightPosX0[i], unity_4LightPosY0[i], unity_4LightPosZ0[i], 1);
				penetratorPositionTracker = mul( unity_WorldToObject, penetratorWorld ).xyz;
				if (length(penetratorPositionTracker)>length(tempPenetratorPositionTracker)) {
					penetratorPositionTracker = tempPenetratorPositionTracker;
				} else {
					penetratorLength=unity_LightColor[i].a;
				}
			}
		}
	}
}
	
	float3 getBlendOffset(float blendSampleIndex, float activationDepth, float activationSmooth, int vertexID, float penetrationDepth, float3 normal, float3 tangent, float3 binormal) {
		float blendTextureSize = 1024;
		float2 blendSampleUV = (float2(( ( fmod( (float)vertexID , blendTextureSize ) + 0.5 ) / (blendTextureSize) ) , ( ( ( floor( ( vertexID / (blendTextureSize) ) ) + 0.5 ) / (blendTextureSize) ) + blendSampleIndex/8 )));
		float3 sampledBlend = tex2Dlod( _OrificeData, float4( blendSampleUV, 0, 0.0) ).rgb;
		float blendActivation = smoothstep( ( activationDepth ) , ( activationDepth + activationSmooth ) , penetrationDepth);
		blendActivation = -cos(blendActivation*3.1416)*0.5+0.5;
		float3 blendOffset = ( ( sampledBlend - float3(1,1,1)) * (blendActivation) * _BlendshapePower * _BlendshapeBadScaleFix );
		return ( ( blendOffset.x * normal ) + ( blendOffset.y * tangent ) + ( blendOffset.z * binormal ) );
	}
	
	#if defined(PENETRATION_SYSTEM_ORIFICE)
		inline void apply_dps_orifice(inout VERTEX_IN v) {
			float penetratorLength = 0.1;
			float penetratorDistance;
			float3 orificePositionTracker = float3(0,0,-100);
			float3 orificeNormalTracker = float3(0,0,-99);
			float3 penetratorPositionTracker = float3(0,0,100);
			float orificeType=0;
			
			GetBestLights(_OrificeChannel, orificeType, orificePositionTracker, orificeNormalTracker, penetratorPositionTracker, penetratorLength);
			penetratorDistance = distance(orificePositionTracker, penetratorPositionTracker );

			float penetrationDepth = (penetratorLength - penetratorDistance);

			float3 normal = normalize( v.normal );
			float3 tangent = normalize( v.tangent.xyz );
			float3 binormal = normalize(cross( normal , tangent ));
			
			v.vertex.xyz += getBlendOffset(0, 0, _EntryOpenDuration, v.vertexId, penetrationDepth, normal, tangent, binormal);
			v.vertex.xyz += getBlendOffset(2, _Shape1Depth, _Shape1Duration, v.vertexId, penetrationDepth, normal, tangent, binormal);
			v.vertex.xyz += getBlendOffset(4, _Shape2Depth, _Shape2Duration, v.vertexId, penetrationDepth, normal, tangent, binormal);
			v.vertex.xyz += getBlendOffset(6, _Shape3Depth, _Shape3Duration, v.vertexId, penetrationDepth, normal, tangent, binormal);
			v.vertex.w = 1;

			v.normal += getBlendOffset(1, 0, _EntryOpenDuration, v.vertexId, penetrationDepth, normal, tangent, binormal);
			v.normal += getBlendOffset(3, _Shape1Depth, _Shape1Duration, v.vertexId, penetrationDepth, normal, tangent, binormal);
			v.normal += getBlendOffset(5, _Shape2Depth, _Shape2Duration, v.vertexId, penetrationDepth, normal, tangent, binormal);
			v.normal += getBlendOffset(7, _Shape3Depth, _Shape3Duration, v.vertexId, penetrationDepth, normal, tangent, binormal);
			v.normal = normalize(v.normal);
		}
	#elif defined(PENETRATION_SYSTEM_PENETRATOR)
		inline void apply_dps_penetrator(inout VERTEX_IN v) {
			float orificeType = 0;
			float3 orificePositionTracker = float3(0,0,100);
			float3 orificeNormalTracker = float3(0,0,99);
			float3 penetratorPositionTracker = float3(0,0,1);
			float pl=0;
			GetBestLights(_OrificeChannel, orificeType, orificePositionTracker, orificeNormalTracker, penetratorPositionTracker, pl);
			float3 orificeNormal = normalize( lerp( ( orificePositionTracker - orificeNormalTracker ) , orificePositionTracker , max( _EntranceStiffness , 0.01 )) );
			float3 PhysicsNormal = normalize(penetratorPositionTracker.xyz) * _Length * 0.3;
			float wriggleTime = _Time.y * _WriggleSpeed;
			float temp_output_257_0 = ( _Length * ( ( cos( wriggleTime ) * _Wriggle ) + _Curvature ) );
			float wiggleTime = _Time.y * ( _WriggleSpeed * 0.39 );
			float distanceToOrifice = length( orificePositionTracker );
			float enterFactor = smoothstep( ( _Length + -0.05 ) , _Length , distanceToOrifice);
			float3 finalOrificeNormal = normalize( lerp( orificeNormal , ( PhysicsNormal + ( ( float3(0,1,0) * ( temp_output_257_0 + ( _Length * ( _ReCurvature + ( ( sin( wriggleTime ) * 0.3 ) * _Wriggle ) ) * 2.0 ) ) ) + ( float3(0.5,0,0) * ( cos( wiggleTime ) * _Wriggle ) ) ) ) , enterFactor) );
			float smoothstepResult186 = smoothstep( _Length , ( _Length + 0.05 ) , distanceToOrifice);
			float3 finalOrificePosition = lerp( orificePositionTracker , ( ( normalize(penetratorPositionTracker) * _Length ) + ( float3(0,0.2,0) * ( sin( ( wriggleTime + UNITY_PI ) ) * _Wriggle ) * _Length ) + ( float3(0.2,0,0) * _Length * ( sin( ( wiggleTime + UNITY_PI ) ) * _Wriggle ) ) ) , smoothstepResult186);
			float finalOrificeDistance = length( finalOrificePosition );
			float3 bezierBasePosition = float3(0,0,0);
			float temp_output_59_0 = ( finalOrificeDistance / 3.0 );
			float3 lerpResult274 = lerp( float3( 0,0,0 ) , ( float3(0,1,0) * ( temp_output_257_0 * -0.2 ) ) , saturate( ( distanceToOrifice / _Length ) ));
			float3 temp_output_267_0 = ( ( temp_output_59_0 * float3(0,0,1) ) + lerpResult274 );
			float3 bezierBaseNormal = temp_output_267_0;
			float3 temp_output_63_0 = ( finalOrificePosition - ( temp_output_59_0 * finalOrificeNormal ) );
			float3 bezierOrificeNormal = temp_output_63_0;
			float3 bezierOrificePosition = finalOrificePosition;
			float vertexBaseTipPosition = ( v.vertex.z / finalOrificeDistance );
			float t = saturate(vertexBaseTipPosition);
			float oneMinusT = 1 - t;
			float3 bezierPoint = oneMinusT * oneMinusT * oneMinusT * bezierBasePosition + 3 * oneMinusT * oneMinusT * t * bezierBaseNormal + 3 * oneMinusT * t * t * bezierOrificeNormal + t * t * t * bezierOrificePosition;
			float3 straightLine = (float3(0.0 , 0.0 , v.vertex.z));
			float baseFactor = smoothstep( 0.05 , -0.05 , v.vertex.z);
			bezierPoint = lerp( bezierPoint , straightLine , baseFactor);
			bezierPoint = lerp( ( ( finalOrificeNormal * ( v.vertex.z - finalOrificeDistance ) ) + finalOrificePosition ) , bezierPoint , step( vertexBaseTipPosition , 1.0 ));
			float3 bezierDerivitive = 3 * oneMinusT * oneMinusT * (bezierBaseNormal - bezierBasePosition) + 6 * oneMinusT * t * (bezierOrificeNormal - bezierBaseNormal) + 3 * t * t * (bezierOrificePosition - bezierOrificeNormal);
			bezierDerivitive = normalize( lerp( bezierDerivitive , float3(0,0,1) , baseFactor) );
			float bezierUpness = dot( bezierDerivitive , float3( 0,1,0 ) );
			float3 bezierUp = lerp( float3(0,1,0) , float3( 0,0,-1 ) , saturate( bezierUpness ));
			float bezierDownness = dot( bezierDerivitive , float3( 0,-1,0 ) );
			bezierUp = normalize( lerp( bezierUp , float3( 0,0,1 ) , saturate( bezierDownness )) );
			float3 bezierSpaceX = normalize( cross( bezierDerivitive , bezierUp ) );
			float3 bezierSpaceY = normalize( cross( bezierDerivitive , -bezierSpaceX ) );
			float3 bezierSpaceVertexOffset = ( ( v.vertex.y * bezierSpaceY ) + ( v.vertex.x * -bezierSpaceX ) );
			float3 bezierSpaceVertexOffsetNormal = normalize( bezierSpaceVertexOffset );
			float distanceFromTip = ( finalOrificeDistance - v.vertex.z );
			float squeezeFactor = smoothstep( 0.0 , _SqueezeDist , -distanceFromTip);
			squeezeFactor = max( squeezeFactor , smoothstep( 0.0 , _SqueezeDist , distanceFromTip));
			float3 bezierSpaceVertexOffsetSqueezed = lerp( ( bezierSpaceVertexOffsetNormal * min( length( bezierSpaceVertexOffset ) , _squeeze ) ) , bezierSpaceVertexOffset , squeezeFactor);
			float bulgeFactor = smoothstep( 0.0 , _BulgeOffset , abs( ( finalOrificeDistance - v.vertex.z ) ));
			float bulgeFactorBaseClip = smoothstep( 0.0 , 0.05 , v.vertex.z);
			float bezierSpaceVertexOffsetBulged = lerp( 1.0 , ( 1.0 + _BulgePower ) , ( ( 1.0 - bulgeFactor ) * 100.0 * bulgeFactorBaseClip ));
			float3 bezierSpaceVertexOffsetFinal = lerp( ( bezierSpaceVertexOffsetSqueezed * bezierSpaceVertexOffsetBulged ) , bezierSpaceVertexOffset , enterFactor);
			float3 bezierConstructedVertex = ( bezierPoint + bezierSpaceVertexOffsetFinal );
			float3 sphereifyDistance = ( bezierConstructedVertex - finalOrificePosition );
			float3 sphereifyNormal = normalize( sphereifyDistance );
			float sphereifyFactor = smoothstep( 0.05 , -0.05 , distanceFromTip);
			float killSphereifyForRing = lerp( sphereifyFactor , 0.0 , orificeType);
			bezierConstructedVertex = lerp( bezierConstructedVertex , ( ( min( length( sphereifyDistance ) , _squeeze ) * sphereifyNormal ) + finalOrificePosition ) , killSphereifyForRing);
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex );
			float3 ase_worldViewDir = normalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			bezierConstructedVertex = lerp( bezierConstructedVertex , ( -ase_worldViewDir * float3( 10000,10000,10000 ) ) , _WorldSpaceLightPos0.w);
			v.normal = normalize( ( ( -bezierSpaceX * v.normal.x ) + ( bezierSpaceY * v.normal.y ) + ( bezierDerivitive * v.normal.z ) ) );
			v.vertex.xyz = bezierConstructedVertex;
			v.vertex.w = 1;
		}
	#endif
#endif

inline void apply_dps(inout VERTEX_IN v) { 
	#if defined(PENETRATION_SYSTEM_ON)
		if (_DPS_Toggle >= 0.5){
			#if defined(PENETRATION_SYSTEM_ORIFICE)
				apply_dps_orifice(v);
			#elif defined(PENETRATION_SYSTEM_PENETRATOR)
				apply_dps_penetrator(v);
			#endif
		}
	#endif
}

#endif // KAWAFLT_FEATURE_PENETRATION_SYSTEM_INCLUDED