using System;
using System.Collections.Generic;
using Kawashirov.ShaderBaking;

namespace Kawashirov.FLT {

	internal static partial class Commons {

		internal static string[] tags = new string[] {
			ShaderBaking.Commons.GenaratorGUID,
			//
			RenderType,
			F_Debug, F_Instancing,
			F_Geometry, F_Tessellation, F_Partitioning, F_Domain,
			F_Random,
			F_MainTex,
			F_Cutout_Forward, F_Cutout_ShadowCaster, F_Cutout_Classic, F_Cutout_RangeRandom,
			F_Emission, F_EmissionMode,
			F_NormalMap,
			F_Shading,
			F_DistanceFade, F_DistanceFadeMode, 
			F_Matcap, F_MatcapMode,
			F_FPS, F_FPSMode,
			F_Outline, F_OutlineMode,
			F_IWD, F_IWDDirections,
			F_PCW, F_PCWMode
		};

	}


}
