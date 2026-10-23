using Godot;

namespace Rempart;

/// <summary>Silhouette du soldat de la maquette E-Arsenal (SVG rastérisé une fois, à la demande).</summary>
public static class ArsenalESoldier
{
    private static ImageTexture _tex;

    /// <summary>Texture du soldat (300 × 540 unités de maquette, rastérisée à l'échelle 2 pour rester nette).</summary>
    public static Texture2D Texture
    {
        get
        {
            if (_tex != null) return _tex;
            var img = new Image();
            if (img.LoadSvgFromString(Svg, 2f) != Error.Ok) return null;
            _tex = ImageTexture.CreateFromImage(img);
            return _tex;
        }
    }

    public const string Svg = """
<svg xmlns="http://www.w3.org/2000/svg" width="300" height="540" viewBox="0 0 300 540">
<defs>
<linearGradient id="eaCamo" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#274031"></stop><stop offset="0.5" stop-color="#46785B"></stop><stop offset="1" stop-color="#25392D"></stop></linearGradient>
</defs>
<path d="M96 118 L204 118 L214 236 L86 236 Z" fill="#1A2820"></path>
<path d="M104 262 L148 262 L144 360 L110 360 Z" fill="url(#eaCamo)"></path>
<path d="M154 262 L198 262 L194 360 L160 360 Z" fill="url(#eaCamo)"></path>
<rect x="96" y="282" width="14" height="36" rx="3" fill="#203328"></rect>
<rect x="192" y="282" width="14" height="36" rx="3" fill="#203328"></rect>
<rect x="106" y="352" width="40" height="30" rx="8" fill="#315440" stroke="#17251D"></rect>
<rect x="156" y="352" width="40" height="30" rx="8" fill="#315440" stroke="#17251D"></rect>
<path d="M110 380 L144 380 L140 472 L114 472 Z" fill="url(#eaCamo)"></path>
<path d="M160 380 L194 380 L190 472 L164 472 Z" fill="url(#eaCamo)"></path>
<path d="M108 470 L144 470 L148 502 L98 502 Q98 482 108 470 Z" fill="#141E18"></path>
<path d="M160 470 L196 470 Q206 482 206 502 L156 502 Z" fill="#141E18"></path>
<path d="M100 236 L204 236 L202 266 L102 266 Z" fill="#1E3026"></path>
<rect x="142" y="242" width="20" height="16" fill="#5C9674"></rect>
<rect x="108" y="240" width="22" height="22" rx="2" fill="#284434"></rect>
<rect x="174" y="240" width="22" height="22" rx="2" fill="#284434"></rect>
<path d="M102 118 Q152 104 200 118 L208 238 L96 238 Z" fill="url(#eaCamo)"></path>
<path d="M114 128 Q152 118 190 128 L194 196 Q152 208 110 196 Z" fill="#3A644B" stroke="#1C2E24" stroke-width="2"></path>
<path d="M152 122 V204 M118 160 H186" stroke="#1C2E24" stroke-width="1.5"></path>
<rect x="114" y="204" width="18" height="28" rx="2" fill="#284434"></rect>
<rect x="134" y="204" width="18" height="28" rx="2" fill="#284434"></rect>
<rect x="154" y="204" width="18" height="28" rx="2" fill="#284434"></rect>
<rect x="174" y="204" width="18" height="28" rx="2" fill="#284434"></rect>
<rect x="140" y="92" width="24" height="18" fill="#1C2E24"></rect>
<path d="M126 106 Q152 98 178 106 L176 120 Q152 112 128 120 Z" fill="#283F32"></path>
<ellipse cx="96" cy="128" rx="24" ry="18" fill="#3A644B" stroke="#1C2E24" stroke-width="2"></ellipse>
<ellipse cx="208" cy="128" rx="24" ry="18" fill="#3A644B" stroke="#1C2E24" stroke-width="2"></ellipse>
<path d="M74 134 L104 134 L104 196 L80 200 Z" fill="url(#eaCamo)"></path>
<path d="M80 190 L104 184 L152 212 L140 232 Z" fill="#2F513D"></path>
<path d="M200 134 L228 134 L224 186 L198 186 Z" fill="url(#eaCamo)"></path>
<path d="M198 180 L224 182 L222 168 L206 158 Z" fill="#2F513D"></path>
<g transform="translate(92 256) rotate(-36)">
<g transform="translate(0 -22) scale(1.65)">
<polygon points="2,18 24,15 32,15 32,29 24,31 6,33 2,31" fill="#1B251F"></polygon>
<rect x="32" y="13" width="44" height="16" fill="#222F27"></rect>
<polygon points="76,15 104,15 104,25 76,26" fill="#1B251F"></polygon>
<rect x="104" y="18" width="16" height="4" fill="#151C18"></rect>
<rect x="118" y="16" width="7" height="8" fill="#151C18"></rect>
<polygon points="58,29 68,29 71,45 61,45" fill="#17211B"></polygon>
<polygon points="40,29 49,29 47,41 39,41" fill="#17211B"></polygon>
<rect x="46" y="5" width="24" height="6" rx="2" fill="#151C18"></rect>
<rect x="53" y="10" width="9" height="4" fill="#151C18"></rect>
<path d="M36 20 H72 M80 20 H100" stroke="#3D5045" stroke-width="1.2"></path>
</g>
</g>
<circle cx="146" cy="222" r="11" fill="#142019"></circle>
<circle cx="212" cy="166" r="11" fill="#142019"></circle>
<path d="M116 74 Q114 26 152 22 Q190 26 188 74 Z" fill="#335642"></path>
<path d="M128 40 Q150 28 172 38" stroke="#518767" stroke-width="3" fill="none"></path>
<path d="M124 80 L180 80 L176 100 Q152 110 128 100 Z" fill="#131B16"></path>
<path d="M110 74 Q152 66 194 74 L192 82 Q152 74 112 82 Z" fill="#243F2F"></path>
<rect x="128" y="84" width="48" height="8" rx="3" fill="#56786C"></rect>
</svg>
""";
}
