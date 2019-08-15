#ifndef KAWAFLT_PREFRAG_SHARED_INCLUDED
#define KAWAFLT_PREFRAG_SHARED_INCLUDED

// Перед включением этого файла должен быть включен файл соотв. pipeline

inline void prefrag_shadowcaster_pos(float3 vertex_obj, float3 normal_obj, inout float4 pos) {
	// В рот ебал разрабов Юнити
	// Переписываем хуёвый макрос TRANSFER_SHADOW_CASTER_NOPOS в функцию,
	// потому что он не работает если g2f не называется `v`, макрос требует `v.vertex` и `v.normal`
	// Пересчёт `pos` нужен для того, что в режиме SHADOWS_DEPTH сдвинуть объекты по Z оси пространства экрана, что бы приминить unity_LightShadowBias.
	// На DX11 на ПК должен быть опередел SHADOWS_CUBE_IN_DEPTH_TEX, тогда никаких доп. переменных для шадоу кастера нет.
	// Зачем нужен normal я до сих пор не понял, используется в UnityClipSpaceShadowCasterPos 
	// В доках юнити упоминается статья http://the-witness.net/news/2013/09/shadow-mapping-summary-part-1/
	// Нихуя не понял, но всёравно интересно.
	#if defined(KAWAFLT_PASS_SHADOWCASTER)
		#if defined(SHADOWS_CUBE) && !defined(SHADOWS_CUBE_IN_DEPTH_TEX)
			#error "defined(SHADOWS_CUBE) && !defined(SHADOWS_CUBE_IN_DEPTH_TEX) - this should not happen on DX11 on PC, pls report this error"
		#else
			pos = UnityClipSpaceShadowCasterPos(vertex_obj, normal_obj);
			pos = UnityApplyLinearShadowBias(pos);
		#endif
	#endif
}


struct __prefrag_transfer_shadow__ {
	float4 vertex : POSITION;
};

inline void prefrag_transfer_shadow(float4 vertex_obj, inout FRAGMENT_IN v_out) {
	#if defined(KAWAFLT_PASS_FORWARD)
		__prefrag_transfer_shadow__ v;
		v.vertex = vertex_obj;
		// Макро юнити обращается к v.vertex, здорово, правда?
		TRANSFER_SHADOW(v_out);  
	#endif
}


#endif // KAWAFLT_PREFRAG_SHARED_INCLUDED