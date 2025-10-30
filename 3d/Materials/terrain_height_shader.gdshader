shader_type spatial;

// Height-based terrain coloring shader
// Shows digging progress: Yellow (high) → Green → Blue → Purple (flat)

uniform float height_max = 0.35;  // Highest peaks
uniform float height_high = 0.20; // High terrain
uniform float height_mid = 0.12;  // Medium terrain
uniform float height_low = 0.05;  // Nearly flat
uniform float height_min = 0.00;  // Completely flat

// Colors for height gradient
uniform vec4 color_peak : source_color = vec4(1.0, 0.9, 0.2, 1.0);    // Yellow - HIGH PEAKS (dig targets!)
uniform vec4 color_high : source_color = vec4(0.3, 0.8, 0.3, 1.0);    // Green - Medium-high
uniform vec4 color_mid : source_color = vec4(0.2, 0.5, 0.8, 1.0);     // Blue - Medium-low
uniform vec4 color_low : source_color = vec4(0.4, 0.2, 0.6, 1.0);     // Purple - Nearly flat
uniform vec4 color_flat : source_color = vec4(0.3, 0.15, 0.5, 1.0);   // Dark purple - FLAT (done!)

void fragment() {
	// Get world Y position (height)
	float height = (MODEL_MATRIX * vec4(VERTEX, 1.0)).y;
	
	vec3 color;
	
	// Interpolate colors based on height
	if (height > height_high) {
		// Yellow to Green (high peaks to medium-high)
		float t = smoothstep(height_high, height_max, height);
		color = mix(color_high.rgb, color_peak.rgb, t);
	}
	else if (height > height_mid) {
		// Green to Blue (medium-high to medium-low)
		float t = smoothstep(height_mid, height_high, height);
		color = mix(color_mid.rgb, color_high.rgb, t);
	}
	else if (height > height_low) {
		// Blue to Purple (medium-low to nearly flat)
		float t = smoothstep(height_low, height_mid, height);
		color = mix(color_low.rgb, color_mid.rgb, t);
	}
	else {
		// Purple to Dark Purple (nearly flat to completely flat)
		float t = smoothstep(height_min, height_low, height);
		color = mix(color_flat.rgb, color_low.rgb, t);
	}
	
	ALBEDO = color;
	ROUGHNESS = 0.9;
	SPECULAR = 0.1;
}
