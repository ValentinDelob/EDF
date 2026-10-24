using System;
using Godot;

namespace Rempart;

/// <summary>
/// Aliens humanoïdes (spécification §10.1) : fantassin (≈ 4,9 m), garde (≈ 5,7 m) et volant (≈ 4,3 m). Silhouettes
/// étirées, carapace d'armure sombre posée sur une chair grise et luisante, casque allongé en arrière, fente de capteurs
/// violets, mandibules. Construits pièce par pièce sur une armature (<see cref="Bone"/>) puis cuits en maillages à os
/// (BugModel.Skin.cs) comme les insectes ; animation procédurale dans BugModel.AlienAnim.cs.
///
/// Repère du modèle : avant = +Z, droite = -X (comme le soldat), mètres. Index des côtés : 0 = droite, 1 = gauche.
/// Les pièces paires sont construites côté gauche (+X) et mises en miroir pour le côté droit.
/// </summary>
public partial class BugModel
{
    /// <summary>Alien humanoïde (fantassin, garde, volant).</summary>
    public bool IsAlienType => Type is EnemyType.AlienTrooper or EnemyType.AlienGuard or EnemyType.AlienFlyer;

    /// <summary>Gabarit d'un alien : longueurs (L) et carrure (W) appliquées au gabarit du fantassin.</summary>
    private sealed class AlienDims
    {
        public float L, W;
        public float HipY, HipX, JointY, Thigh, Shin, Ankle;
        public float SpineY, ChestY, NeckY, NeckZ;
        public float ShoulderX, ShoulderY, Upper, Fore;
        /// <summary>Arme (repère de l'arme) : poignée, garde-main, crosse, bouche.</summary>
        public Vector3 Grip, Support, Butt, Muzzle;
        /// <summary>Appui de la crosse (repère du torse).</summary>
        public Vector3 Pocket;
        /// <summary>Centre de prise de la main (repère de la main).</summary>
        public Vector3 GripC;
    }

    private AlienDims _ad;
    private Bone _aHips, _aSpine, _aChest, _aHead, _aGun, _aMuzzle, _aShield, _aCore;
    private readonly Bone[] _aThigh = new Bone[2], _aShin = new Bone[2], _aFoot = new Bone[2];
    private readonly Bone[] _aArm = new Bone[2], _aFore = new Bone[2], _aHand = new Bone[2];
    private readonly Bone[] _aFlame = new Bone[2];

    // ---------------- Palette ----------------

    /// <summary>Carapace d'armure : ardoise brun sombre.</summary>
    private static readonly Color ACarap = new(0.31f, 0.29f, 0.25f);
    /// <summary>Plaques secondaires, un ton plus clair (arêtes, épaulières).</summary>
    private static readonly Color ACarap2 = new(0.43f, 0.405f, 0.355f);
    /// <summary>Joints, mécanique noircie.</summary>
    private static readonly Color ATrim = new(0.07f, 0.065f, 0.07f);
    /// <summary>Chair grise et mauve.</summary>
    private static readonly Color AFlesh = new(0.5f, 0.43f, 0.44f);
    /// <summary>Chair sombre (creux, tendons).</summary>
    private static readonly Color AFleshD = new(0.3f, 0.21f, 0.26f);
    /// <summary>Griffes, mandibules, épines : os jauni.</summary>
    private static readonly Color ABone = new(0.64f, 0.6f, 0.5f);

    /// <summary>Énergie des aliens : violet pâle (traits, capteurs, réacteurs, portail).</summary>
    public static readonly Color AlienEnergy = new(0.72f, 0.42f, 1f);

    // ---------------- Matériaux ----------------

    private static ShaderMaterial _alArmor, _alArmorElite;
    private static StandardMaterial3D _alFlesh, _alGlow, _alFlame;

    /// <summary>Armure des aliens : même usure en repère local que les machines, plus lisse et sans rouille.</summary>
    private static ShaderMaterial AlienArmor() => _alArmor ??= MachMat(Vector3.One, 0.35f, 0.46f, 0.95f, 0.08f, 0.45f);

    /// <summary>Variante élite : laque rouge sang sur l'armure.</summary>
    private static ShaderMaterial AlienArmorElite() => _alArmorElite ??= MachMat(new Vector3(1.55f, 0.42f, 0.36f), 0.45f, 0.3f, 0.6f, 0f, 0.35f);

    /// <summary>Chair : couleurs de sommets, aspect humide (vernis), léger liseré.</summary>
    private static StandardMaterial3D AlienFlesh() => _alFlesh ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        Roughness = 0.38f,
        ClearcoatEnabled = true,
        Clearcoat = 0.55f,
        ClearcoatRoughness = 0.3f,
        RimEnabled = true,
        Rim = 0.18f,
        RimTint = 0.6f,
    };

    private static StandardMaterial3D AlienGlow() => _alGlow ??= Mats.Glow(AlienEnergy, 4.5f);

    /// <summary>Flamme de réacteur : additive, intensité par couleur de sommet (cône intérieur vif, gaine plus pâle).</summary>
    private static StandardMaterial3D AlienFlameMat() => _alFlame ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(0.82f, 0.62f, 1f),
        VertexColorUseAsAlbedo = true,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
    };

    // ---------------- Construction ----------------

    private void BuildAlien()
    {
        var guard = Type == EnemyType.AlienGuard;
        var flyer = Type == EnemyType.AlienFlyer;
        var L = guard ? 1.1f : flyer ? 0.9f : 1f;
        var W = guard ? 1.12f : flyer ? 0.92f : 1f;
        var d = _ad = new AlienDims
        {
            L = L, W = W,
            HipY = 2.58f * L, HipX = 0.3f * L * W, JointY = -0.06f * L, Thigh = 1.22f * L, Shin = 1.2f * L, Ankle = 0.15f * L,
            SpineY = 0.2f * L, ChestY = 0.62f * L, NeckY = 0.98f * L, NeckZ = 0.06f * L,
            ShoulderX = 0.64f * L * W, ShoulderY = 0.8f * L, Upper = 1.0f * L, Fore = 0.95f * L,
            GripC = new Vector3(0, -0.29f, 0.1f) * L,
        };
        // Sections : largeur × carrure, hauteur × longueur.
        var S = new Transform3D(Basis.FromScale(new Vector3(L * W, L, L * W)), Vector3.Zero);
        _pivotY = d.HipY;

        _aHips = new Bone { Position = new Vector3(0, d.HipY, 0) };
        AddChild(_aHips);
        Part(_aHips, "pelvis", m => { m.Xf = S; AlienPelvis(m, guard); });

        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? -1f : 1f;
            var key = i == 0 ? "R" : "L";
            _aThigh[i] = new Bone { Position = new Vector3(d.HipX * side, d.JointY, 0) };
            _aHips.AddChild(_aThigh[i]);
            Part(_aThigh[i], "thigh" + key, m => { m.Xf = S; m.Mirror = side < 0; AlienThigh(m, guard, flyer); });
            _aShin[i] = new Bone { Position = new Vector3(0, -d.Thigh, 0) };
            _aThigh[i].AddChild(_aShin[i]);
            Part(_aShin[i], "shin" + key, m => { m.Xf = S; m.Mirror = side < 0; AlienShin(m, guard, flyer); });
            _aFoot[i] = new Bone { Position = new Vector3(0, -d.Shin, 0) };
            _aShin[i].AddChild(_aFoot[i]);
            Part(_aFoot[i], "foot" + key, m => { m.Xf = S; m.Mirror = side < 0; AlienFoot(m); });
        }

        _aSpine = new Bone { Position = new Vector3(0, d.SpineY, 0) };
        _aHips.AddChild(_aSpine);
        Part(_aSpine, "abdomen", m => { m.Xf = S; AlienAbdomen(m, guard); });
        _aChest = new Bone { Position = new Vector3(0, d.ChestY, 0) };
        _aSpine.AddChild(_aChest);
        Part(_aChest, "chest", m => { m.Xf = S; AlienChest(m, guard, flyer); });
        _aHead = new Bone { Position = new Vector3(0, d.NeckY, d.NeckZ) };
        _aChest.AddChild(_aHead);
        Part(_aHead, "head", m => { m.Xf = S; AlienHead(m, guard, flyer); });

        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? -1f : 1f;
            var key = i == 0 ? "R" : "L";
            _aArm[i] = new Bone { Position = new Vector3(d.ShoulderX * side, d.ShoulderY, 0) };
            _aChest.AddChild(_aArm[i]);
            Part(_aArm[i], "arm" + key, m => { m.Xf = S; m.Mirror = side < 0; AlienUpperArm(m, guard); });
            _aFore[i] = new Bone { Position = new Vector3(0, -d.Upper, 0) };
            _aArm[i].AddChild(_aFore[i]);
            Part(_aFore[i], "fore" + key, m => { m.Xf = S; m.Mirror = side < 0; AlienForearm(m, guard); });
            _aHand[i] = new Bone { Position = new Vector3(0, -d.Fore, 0) };
            _aFore[i].AddChild(_aHand[i]);
            Part(_aHand[i], "hand" + key, m => { m.Xf = S; m.Mirror = side < 0; AlienHand(m); });
        }

        // Arme (repère de l'arme : +Z vers l'avant), portée par le torse ; les mains la rejoignent par cinématique inverse.
        _aGun = new Bone();
        _aChest.AddChild(_aGun);
        var U = new Transform3D(Basis.FromScale(Vector3.One * L), Vector3.Zero);
        if (guard)
        {
            Part(_aGun, "blaster", m => { m.Xf = U; AlienBlaster(m); });
            d.Grip = new Vector3(0, -0.3f, -0.05f) * L;
            d.Support = new Vector3(0, -0.3f, -0.05f) * L;
            d.Butt = new Vector3(0, 0, -0.5f) * L;
            d.Muzzle = new Vector3(0, 0.03f, 1.12f) * L;
            // Bouclier tenu par la main gauche devant le torse.
            _aShield = new Bone { Position = new Vector3(0.3f, 0.1f, 1.0f) * L };
            _aChest.AddChild(_aShield);
            Part(_aShield, "shield", m => { m.Xf = U; AlienShieldMesh(m); });
        }
        else
        {
            Part(_aGun, flyer ? "carbine" : "rifle", m => { m.Xf = new Transform3D(Basis.FromScale(Vector3.One * L * 0.86f), Vector3.Zero); AlienRifle(m, flyer); });
            d.Grip = new Vector3(0, -0.3f, -0.16f) * L * 0.86f;
            d.Support = new Vector3(0, -0.2f, flyer ? 0.55f : 0.78f) * L * 0.86f;
            d.Butt = new Vector3(0, 0, -0.78f) * L * 0.86f;
            d.Muzzle = new Vector3(0, 0.04f, flyer ? 1.45f : 1.98f) * L * 0.86f;
        }
        d.Pocket = new Vector3(-0.38f, 0.62f, 0.32f) * L;
        _aMuzzle = new Bone { Position = d.Muzzle };
        _aGun.AddChild(_aMuzzle);
        // Point faible du garde : la cellule d'énergie dans le dos (centre de la forme de collision).
        _aCore = new Bone { Position = new Vector3(0, 0.45f, guard ? -0.66f : -0.55f) * L };
        _aChest.AddChild(_aCore);

        if (flyer)
        {
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                _aFlame[i] = new Bone { Position = new Vector3(0.27f * side, -0.08f, -0.58f) * L };
                _aChest.AddChild(_aFlame[i]);
                Part(_aFlame[i], "flame", m =>
                {
                    m.Xf = U;
                    m.Use(AlienFlameMat());
                    m.Cone(new Vector3(0, 0, 0), new Vector3(0, -0.8f, 0), 0.08f, 8, new Color(1f, 1f, 1f, 1f), true);
                    m.Cone(new Vector3(0, 0.02f, 0), new Vector3(0, -1.7f, 0), 0.14f, 10, new Color(0.6f, 0.45f, 0.9f, 0.55f), true);
                }, false);
            }
        }

        AnimateAlienRig(0.016f, 0f, 0f);
    }

    // ---------------- Pièces (gabarit du fantassin, côté gauche +X) ----------------

    /// <summary>Plaque dont l'axe Z local descend (le long d'un membre) et l'axe Y regarde vers <paramref name="normal"/>.</summary>
    private static void HangPlate(InsectMesh m, Vector3 c, Vector3 normal, Vector3 half, Color col, float bev = 0.04f, float taper = 0.8f, float lean = 0f, float sq = 5f)
    {
        var y = normal.Normalized();
        var z = (Vector3.Down - y * Vector3.Down.Dot(y)).Normalized();
        if (lean != 0f) z = (z + y * lean).Normalized();
        y = (y - z * y.Dot(z)).Normalized();
        var x = y.Cross(z);
        Plate(m, new Transform3D(new Basis(x, y, z), c), half, col, bev, sq, taper);
    }

    /// <summary>
    /// Coque de carapace : ellipsoïde aplati (demi-axes <paramref name="r"/> : largeur, épaisseur selon
    /// <paramref name="normal"/>, longueur selon <paramref name="along"/>), à moitié enfoncé dans la chair. Bords bombés,
    /// aucun angle droit : une armure organique plutôt que des plaques de jouet.
    /// </summary>
    private static void Shell(InsectMesh m, Vector3 c, Vector3 normal, Vector3 along, Vector3 r, Color col)
    {
        var y = normal.Normalized();
        var z = (along - y * along.Dot(y)).Normalized();
        var x = y.Cross(z);
        m.Ellipsoid(c, r, 7, 14, col, new Basis(x, y, z), (_, _, p, cc) => cc * (0.93f + 0.1f * Mathf.Sin(p.X * 17f + p.Y * 9f + p.Z * 13f)));
    }

    /// <summary>Peinture de la chair : veines sombres et marbrures (bruit de hachage sur la position).</summary>
    private static Color FleshPaint(Vector3 p, Color c)
    {
        var n = Mathf.Sin(p.X * 23.1f + p.Y * 7.7f) * Mathf.Sin(p.Y * 17.3f - p.Z * 11.9f) * Mathf.Sin(p.Z * 19.7f + p.X * 5.3f);
        var vein = Mathf.Abs(Mathf.Sin(p.Y * 31f + Mathf.Sin(p.X * 9f) * 2f + p.Z * 13f)) < 0.08f ? 0.55f : 1f;
        return c * (0.88f + 0.18f * n) * vein;
    }

    private static void AlienPelvis(InsectMesh m, bool guard)
    {
        m.Use(AlienFlesh());
        m.Ellipsoid(new Vector3(0, -0.02f, 0), new Vector3(0.36f, 0.27f, 0.26f), 6, 14, AFleshD, null, (_, _, p, c) => FleshPaint(p, c));
        m.Use(AlienArmor());
        // Ceinture de carapace bombée, rebord sombre ; faude, tassettes et plaque de reins en coques galbées.
        m.Ellipsoid(new Vector3(0, 0.06f, 0), new Vector3(0.42f, 0.15f, 0.3f), 6, 18, ACarap, null, (_, _, p, c) => Mathf.Abs(p.Y - 0.06f) < 0.025f ? ATrim : c);
        Shell(m, new Vector3(0, -0.17f, 0.24f), new Vector3(0, -0.2f, 1), Vector3.Down, new Vector3(0.17f, 0.07f, 0.2f), ACarap2);
        for (var s = -1; s <= 1; s += 2)
        {
            Shell(m, new Vector3(0.37f * s, -0.17f, 0.02f), new Vector3(s, -0.25f, 0), Vector3.Down, new Vector3(0.19f, 0.07f, 0.21f), ACarap);
            if (guard) Shell(m, new Vector3(0.41f * s, -0.33f, 0.02f), new Vector3(s, -0.3f, 0), Vector3.Down, new Vector3(0.18f, 0.06f, 0.18f), ACarap2);
        }
        Shell(m, new Vector3(0, -0.1f, -0.24f), new Vector3(0, -0.2f, -1), Vector3.Down, new Vector3(0.27f, 0.07f, 0.18f), ACarap);
        m.Use(MachMech());
        for (var s = -1; s <= 1; s += 2)
            Cyl(m, new Vector3(0.22f * s, -0.06f, 0), new Vector3(0.36f * s, -0.06f, 0), 0.15f, 12, ATrim);
    }

    private static void AlienThigh(InsectMesh m, bool guard, bool flyer)
    {
        m.Use(AlienFlesh());
        m.Segment(new Vector3(0, 0.06f, 0), new Vector3(0, -1.18f, 0.01f), 0.25f, 0.14f, 12, AFlesh, Vector3.Back, 0.12f, 4,
            t => 1f + 0.14f * Mathf.Sin(t * Mathf.Pi) - 0.05f * t, t => FleshPaint(new Vector3(0, -t, t), AFlesh.Lerp(AFleshD, t * 0.6f)));
        // Tendons à l'arrière de la cuisse.
        for (var s = -1; s <= 1; s += 2)
            m.Tube(new[] { new Vector3(0.08f * s, -0.15f, -0.16f), new Vector3(0.09f * s, -0.6f, -0.18f), new Vector3(0.06f * s, -1.05f, -0.1f) },
                new[] { 0.04f, 0.05f, 0.03f }, 6, AFleshD, Vector3.Back);
        m.Use(AlienArmor());
        // Cuissard : coque frontale galbée à arête médiane, coque latérale, écaille au-dessus du genou.
        Shell(m, new Vector3(0, -0.45f, 0.16f), new Vector3(0, 0, 1), Vector3.Down, new Vector3(0.2f, 0.11f, flyer ? 0.34f : 0.44f), ACarap);
        m.Tube(new[] { new Vector3(0, -0.1f, 0.25f), new Vector3(0, -0.45f, 0.275f), new Vector3(0, -0.8f, 0.24f) }, new[] { 0.022f, 0.03f, 0.015f }, 5, ACarap2, Vector3.Right, 1.6f);
        Shell(m, new Vector3(0.18f, -0.36f, -0.01f), new Vector3(1, 0, 0.2f), Vector3.Down, new Vector3(0.15f, 0.08f, 0.32f), ACarap2);
        Shell(m, new Vector3(0, -0.98f, 0.13f), new Vector3(0, 0.1f, 1), Vector3.Down, new Vector3(0.15f, 0.06f, 0.13f), ACarap2);
        if (guard) Shell(m, new Vector3(-0.12f, -0.4f, 0.03f), new Vector3(-1, 0, 0.3f), Vector3.Down, new Vector3(0.13f, 0.07f, 0.3f), ACarap);
        m.Use(MachMech());
        Cyl(m, new Vector3(-0.16f, -1.22f, 0), new Vector3(0.16f, -1.22f, 0), 0.13f, 12, ATrim);
    }

    private static void AlienShin(InsectMesh m, bool guard, bool flyer)
    {
        m.Use(AlienFlesh());
        // Mollet (chair) à l'arrière, sous la grève.
        m.Ellipsoid(new Vector3(0, -0.36f, -0.06f), new Vector3(0.14f, 0.32f, 0.13f), 6, 10, AFlesh, null, (_, _, p, c) => FleshPaint(p, c));
        m.Segment(new Vector3(0, 0, 0), new Vector3(0, -1.16f, 0), 0.15f, 0.1f, 10, AFleshD, Vector3.Back, 0.15f, 3);
        m.Use(AlienArmor());
        // Grève : coque frontale effilée vers la cheville, arête, coque latérale.
        Shell(m, new Vector3(0, -0.5f, 0.07f), new Vector3(0, 0, 1), Vector3.Down, new Vector3(0.15f, 0.1f, 0.5f), ACarap);
        m.Tube(new[] { new Vector3(0, -0.12f, 0.17f), new Vector3(0, -0.5f, 0.175f), new Vector3(0, -0.92f, 0.12f) }, new[] { 0.02f, 0.026f, 0.012f }, 5, ACarap2, Vector3.Right, 1.6f);
        Shell(m, new Vector3(0.09f, -0.42f, 0.0f), new Vector3(1, 0, 0.2f), Vector3.Down, new Vector3(0.11f, 0.07f, 0.34f), ACarap2);
        // Genouillère bombée, épine de genou.
        m.Ellipsoid(new Vector3(0, 0.02f, 0.15f), new Vector3(0.16f, 0.18f, 0.11f), 5, 10, ACarap2);
        m.Use(MachMech());
        m.Cone(new Vector3(0, 0.08f, 0.22f), new Vector3(0, guard ? 0.34f : 0.26f, 0.42f), 0.055f, 6, ABone, true);
        Cyl(m, new Vector3(0, -1.1f, 0), new Vector3(0, -1.2f, 0), 0.11f, 10, ATrim);
    }

    private static void AlienFoot(InsectMesh m)
    {
        m.Use(MachMech());
        m.Ellipsoid(Vector3.Zero, new Vector3(0.12f, 0.12f, 0.12f), 5, 10, ATrim);
        m.Use(AlienArmor());
        // Pied long et étroit, trois orteils griffus, ergot au talon ; semelle à -0,15.
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, -0.075f, 0.2f)), new Vector3(0.13f, 0.075f, 0.36f), ACarap, 0.04f, 6f, 0.6f, 0.7f);
        m.Use(MachMech());
        for (var k = -1; k <= 1; k++)
        {
            var x = k * 0.075f;
            m.Hook(new[] { new Vector3(x, -0.08f, 0.5f), new Vector3(x * 1.25f, -0.09f, 0.66f), new Vector3(x * 1.4f, -0.14f, 0.8f) }, 0.05f, 6, ABone, Vector3.Up);
        }
        m.Cone(new Vector3(0, -0.05f, -0.1f), new Vector3(0, -0.13f, -0.32f), 0.06f, 6, ATrim, true);
    }

    private static void AlienAbdomen(InsectMesh m, bool guard)
    {
        m.Use(AlienFlesh());
        var rings = new System.Collections.Generic.List<InsectMesh.Ring>();
        for (var i = 0; i <= 8; i++)
        {
            var t = i / 8f;
            var y = Mathf.Lerp(-0.12f, 0.66f, t);
            var r = Mathf.Lerp(0.3f, 0.34f, t) * (i == 0 || i == 8 ? 0.85f : 1f);
            rings.Add(new InsectMesh.Ring(new Vector3(0, y, 0.01f), Vector3.Right, Vector3.Back, r, r * 0.74f, AFleshD));
        }
        // Côtes : bourrelets de chair pâle et sillons sombres.
        m.Loft(rings, 16, 2.4f, 0f, (i, _, p, c) => FleshPaint(p, i % 2 == 0 ? c : AFlesh), (i, _) => i % 2 == 0 ? 0.94f : 1.04f);
        m.Use(AlienArmor());
        HangPlate(m, new Vector3(0, 0.55f, 0.25f), new Vector3(0, -0.1f, 1), new Vector3(0.2f, 0.035f, 0.11f), ACarap2, 0.03f, 0.85f, 0.12f);
        if (guard)
            HangPlate(m, new Vector3(0, 0.3f, 0.26f), new Vector3(0, -0.1f, 1), new Vector3(0.18f, 0.035f, 0.11f), ACarap, 0.03f, 0.85f, 0.12f);
        for (var s = -1; s <= 1; s += 2)
            HangPlate(m, new Vector3(0.27f * s, 0.3f, 0.06f), new Vector3(s, 0, 0.35f), new Vector3(0.16f, 0.03f, 0.25f), guard ? ACarap2 : ACarap, 0.03f, 0.75f);
        m.Use(MachMech());
        // Colonne : vertèbres mécaniques dans le dos.
        for (var k = 0; k < 4; k++)
            Plate(m, new Transform3D(Basis.Identity, new Vector3(0, -0.05f + k * 0.19f, -0.25f)), new Vector3(0.09f, 0.06f, 0.07f), ATrim, 0.02f, 6f);
    }

    private static void AlienChest(InsectMesh m, bool guard, bool flyer)
    {
        m.Use(AlienArmor());
        // Cage thoracique en carapace (loft vertical), joints de plaques sombres.
        float[] ys = { -0.08f, 0.1f, 0.3f, 0.52f, 0.72f, 0.86f, 0.98f };
        float[] wx = { 0.38f, 0.5f, 0.6f, 0.66f, 0.63f, 0.47f, 0.27f };
        float[] dz = { 0.28f, 0.35f, 0.41f, 0.43f, 0.39f, 0.3f, 0.2f };
        var rings = new System.Collections.Generic.List<InsectMesh.Ring>();
        rings.Add(new InsectMesh.Ring(new Vector3(0, ys[0] - 0.02f, 0), Vector3.Right, Vector3.Back, 0, 0, ACarap));
        for (var i = 0; i < ys.Length; i++)
            rings.Add(new InsectMesh.Ring(new Vector3(0, ys[i], 0.02f), Vector3.Right, Vector3.Back, wx[i] * (guard ? 1.06f : 1f), dz[i], ACarap));
        rings.Add(new InsectMesh.Ring(new Vector3(0, ys[^1] + 0.02f, 0.02f), Vector3.Right, Vector3.Back, 0, 0, ACarap));
        m.Loft(rings, 22, 2.6f, 0f, (_, th, p, c) =>
        {
            var seam = Mathf.Abs(Mathf.Sin(p.Y * 16f)) < 0.09f || Mathf.Abs(Mathf.Cos(th * 3f)) < 0.05f;
            return seam ? ATrim : c * (0.92f + 0.12f * Mathf.Sin(p.X * 11f + p.Y * 5f));
        });
        // Pectoraux en deux plaques, carène sternale.
        for (var s = -1; s <= 1; s += 2)
            HangPlate(m, new Vector3(0.26f * s, 0.58f, 0.37f), new Vector3(0.35f * s, 0.25f, 1f), new Vector3(0.23f, 0.04f, 0.24f), ACarap2, 0.035f, 0.78f, 0.1f);
        HangPlate(m, new Vector3(0, 0.36f, 0.45f), new Vector3(0, 0.1f, 1f), new Vector3(0.05f, 0.05f, 0.36f), ACarap2, 0.02f, 0.6f);
        // Écailles des flancs, comme des côtes.
        for (var s = -1; s <= 1; s += 2)
            for (var k = 0; k < 3; k++)
                HangPlate(m, new Vector3(0.55f * s - 0.03f * k * s, 0.12f + k * 0.17f, 0.08f), new Vector3(s, -0.1f, 0.35f), new Vector3(0.15f, 0.025f, 0.1f), k == 1 ? ACarap2 : ACarap, 0.02f, 0.8f, 0.2f);
        if (guard)
        {
            // Plastron lourd rivé par-dessus.
            HangPlate(m, new Vector3(0, 0.42f, 0.47f), new Vector3(0, 0.15f, 1f), new Vector3(0.42f, 0.05f, 0.36f), ACarap, 0.05f, 0.82f, 0.05f);
            for (var s = -1; s <= 1; s += 2)
                for (var k = 0; k < 3; k++)
                    m.Ellipsoid(new Vector3(0.33f * s, 0.62f - k * 0.2f, 0.52f), new Vector3(0.03f, 0.03f, 0.02f), 2, 6, ATrim);
        }
        // Gorgerin haut autour du cou.
        for (var k = -2; k <= 2; k++)
        {
            var a = k * 0.55f;
            HangPlate(m, new Vector3(Mathf.Sin(a) * 0.27f, 0.98f, Mathf.Cos(a) * 0.2f - 0.02f), new Vector3(Mathf.Sin(a), -0.55f, Mathf.Cos(a)),
                new Vector3(0.12f, 0.03f, 0.15f), k == 0 ? ACarap2 : ACarap, 0.025f, 0.85f);
        }
        // Dos : arête dorsale épineuse, unité d'énergie (cellule violette), durites.
        m.Use(MachMech());
        for (var k = 0; k < 4; k++)
            m.Cone(new Vector3(0, 0.15f + k * 0.2f, -0.36f), new Vector3(0, 0.3f + k * 0.2f, -0.6f), 0.05f, 6, ATrim, true);
        for (var s = -1; s <= 1; s += 2)
        {
            Cyl(m, new Vector3(0.48f * s, 0.78f, 0), new Vector3(0.62f * s, 0.78f, 0), 0.18f, 12, ATrim);
            m.Tube(new[] { new Vector3(0.22f * s, 0.25f, -0.52f), new Vector3(0.36f * s, 0.12f, -0.4f), new Vector3(0.44f * s, 0.05f, -0.18f) },
                new[] { 0.045f, 0.05f, 0.045f }, 6, ATrim, Vector3.Up);
        }
        m.Use(AlienArmor());
        if (flyer)
        {
            // Réacteurs dorsaux : deux tuyères, ailerons latéraux.
            Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.45f, -0.46f)), new Vector3(0.24f, 0.3f, 0.1f), ACarap2, 0.04f, 6f);
            for (var s = -1; s <= 1; s += 2)
            {
                Cyl(m, new Vector3(0.27f * s, 0.78f, -0.58f), new Vector3(0.27f * s, 0.02f, -0.58f), 0.14f, 14, ACarap2, -1f, 0.12f);
                HangPlate(m, new Vector3(0.5f * s, 0.62f, -0.52f), new Vector3(0.2f * s, 0.3f, -1f), new Vector3(0.05f, 0.025f, 0.3f), ACarap, 0.02f, 0.4f, -0.6f);
            }
            m.Use(MachMech());
            for (var s = -1; s <= 1; s += 2)
                Cyl(m, new Vector3(0.27f * s, 0.04f, -0.58f), new Vector3(0.27f * s, -0.1f, -0.58f), 0.11f, 14, ATrim, -1f, 0.15f);
            m.Use(AlienGlow());
            for (var s = -1; s <= 1; s += 2)
                m.Torus(new Vector3(0.27f * s, 0.2f, -0.58f), Basis.Identity, 0.145f, 0.018f, 16, 4, Colors.White);
        }
        else
        {
            Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.48f, guard ? -0.5f : -0.44f)), new Vector3(0.3f, 0.3f, 0.1f), ACarap2, 0.04f, 6f);
            m.Use(AlienGlow());
            if (guard)
            {
                // Cellule d'énergie exposée (point faible) sous une cage.
                m.Ellipsoid(new Vector3(0, 0.45f, -0.66f), new Vector3(0.2f, 0.26f, 0.1f), 5, 12, Colors.White);
                m.Use(MachMech());
                for (var k = -1; k <= 1; k++)
                    Cyl(m, new Vector3(k * 0.12f, 0.18f, -0.68f), new Vector3(k * 0.12f, 0.72f, -0.68f), 0.025f, 6, ATrim);
            }
            else
                for (var s = -1; s <= 1; s += 2)
                    m.Box(new Transform3D(Basis.Identity, new Vector3(0.12f * s, 0.48f, -0.55f)), new Vector3(0.025f, 0.2f, 0.012f), Colors.White);
        }
    }

    private static void AlienHead(InsectMesh m, bool guard, bool flyer)
    {
        m.Use(AlienFlesh());
        m.Tube(new[] { new Vector3(0, -0.2f, -0.03f), new Vector3(0, 0.02f, 0.01f), new Vector3(0, 0.2f, 0.05f) }, new[] { 0.15f, 0.13f, 0.12f }, 10, AFleshD, Vector3.Back);
        for (var s = -1; s <= 1; s += 2)
            m.Tube(new[] { new Vector3(0.09f * s, -0.15f, 0.06f), new Vector3(0.1f * s, 0.05f, 0.1f), new Vector3(0.07f * s, 0.18f, 0.12f) }, new[] { 0.035f, 0.04f, 0.03f }, 6, AFlesh, Vector3.Back);
        m.Use(AlienArmor());
        if (guard)
        {
            // Heaume fermé, massif, à crête basse.
            m.Tube(new[] { new Vector3(0, 0.12f, 0.1f), new Vector3(0, 0.3f, 0.06f), new Vector3(0, 0.46f, -0.04f), new Vector3(0, 0.56f, -0.2f), new Vector3(0, 0.58f, -0.32f) },
                new[] { 0.19f, 0.22f, 0.21f, 0.17f, 0.07f }, 14, ACarap, Vector3.Up, 1.15f, null, 3f);
            HangPlate(m, new Vector3(0, 0.26f, 0.27f), new Vector3(0, 0.15f, 1f), new Vector3(0.17f, 0.04f, 0.22f), ACarap2, 0.04f, 0.7f, 0.25f);
            for (var s = -1; s <= 1; s += 2)
                HangPlate(m, new Vector3(0.2f * s, 0.24f, 0.08f), new Vector3(s, 0, 0.3f), new Vector3(0.15f, 0.03f, 0.2f), ACarap, 0.03f, 0.75f, 0.2f);
            m.Use(AlienGlow());
            m.Box(new Transform3D(Basis.Identity, new Vector3(0, 0.31f, 0.305f)), new Vector3(0.012f, 0.1f, 0.01f), Colors.White);
            return;
        }
        // Crâne casqué allongé vers l'arrière, en carapace à trois écailles qui se chevauchent.
        var len = flyer ? 0.85f : 1f;
        m.Tube(new[] { new Vector3(0, 0.12f, 0.12f), new Vector3(0, 0.28f, 0.1f), new Vector3(0, 0.42f, 0.0f), new Vector3(0, 0.5f, -0.15f * len), new Vector3(0, 0.54f, -0.28f * len), new Vector3(0, 0.55f, -0.38f * len) },
            new[] { 0.15f, 0.18f, 0.18f, 0.15f, 0.1f, 0.03f }, 14, ACarap, Vector3.Up, 1.2f, null, 2.4f);
        Vector3[] scaleC = { new(0, 0.63f, 0.0f), new(0, 0.665f, -0.08f * len), new(0, 0.655f, -0.23f * len) };
        Vector3[] scaleN = { new(0, 1f, 0.1f), new(0, 0.88f, 0.47f), new(0, 0.95f, 0.3f) };
        for (var k = 0; k < 3; k++)
            SurfPlate(m, scaleC[k] + scaleN[k] * 0.012f, scaleN[k], new Vector3(0, -0.3f, -1f), new Vector3(0.13f - 0.025f * k, 0.022f, 0.13f), k == 1 ? ACarap2 : ACarap, 0.02f, 0.7f);
        // Mâchoire de chair sous le masque.
        m.Use(AlienFlesh());
        m.Ellipsoid(new Vector3(0, 0.05f, 0.16f), new Vector3(0.11f, 0.1f, 0.12f), 5, 10, AFlesh, null, (_, _, p, c) => FleshPaint(p, c));
        m.Use(AlienArmor());
        // Masque facial : plaque étroite, menton en avant ; pommettes.
        HangPlate(m, new Vector3(0, 0.23f, 0.26f), new Vector3(0, 0.25f, 1f), new Vector3(0.115f, 0.035f, 0.17f), ACarap2, 0.035f, 0.55f, 0.3f);
        for (var s = -1; s <= 1; s += 2)
            HangPlate(m, new Vector3(0.15f * s, 0.25f, 0.15f), new Vector3(s, 0.1f, 0.45f), new Vector3(0.1f, 0.025f, 0.15f), ACarap, 0.025f, 0.6f, 0.2f);
        // Mandibules (os jauni) sous le masque.
        m.Use(MachMech());
        for (var s = -1; s <= 1; s += 2)
            m.Hook(new[] { new Vector3(0.1f * s, 0.12f, 0.24f), new Vector3(0.13f * s, 0.0f, 0.3f), new Vector3(0.1f * s, -0.12f, 0.33f), new Vector3(0.06f * s, -0.18f, 0.3f) }, 0.04f, 6, ABone, Vector3.Back);
        // Épines arrière du crâne.
        for (var s = -1; s <= 1; s += 2)
            m.Cone(new Vector3(0.1f * s, 0.5f, -0.25f * len), new Vector3(0.22f * s, 0.55f, -0.55f * len), 0.035f, 5, ATrim, true);
        // Capteurs : fente horizontale et rangée verticale au front.
        m.Use(AlienGlow());
        m.Box(new Transform3D(new Basis(Vector3.Right, -0.25f), new Vector3(0, 0.29f, 0.292f)), new Vector3(flyer ? 0.13f : 0.085f, 0.011f, 0.008f), Colors.White);
        for (var s = -1; s <= 1; s += 2)
            m.Ellipsoid(new Vector3(0.11f * s, 0.2f, 0.24f), new Vector3(0.016f, 0.012f, 0.01f), 2, 6, Colors.White);
    }

    /// <summary>Plaque posée sur une surface : normale <paramref name="normal"/>, longueur (axe Z local) selon <paramref name="along"/>.</summary>
    private static void SurfPlate(InsectMesh m, Vector3 c, Vector3 normal, Vector3 along, Vector3 half, Color col, float bev = 0.03f, float taper = 0.8f)
    {
        var y = normal.Normalized();
        var z = (along - y * along.Dot(y)).Normalized();
        var x = y.Cross(z);
        Plate(m, new Transform3D(new Basis(x, y, z), c), half, col, bev, 8f, taper);
    }

    private static void AlienUpperArm(InsectMesh m, bool guard)
    {
        m.Use(AlienFlesh());
        m.Segment(new Vector3(0, 0.05f, 0), new Vector3(0, -0.97f, 0), 0.19f, 0.13f, 12, AFlesh, Vector3.Back, 0.12f, 4,
            t => 1f + 0.24f * Mathf.Sin(t * Mathf.Pi * 0.9f), t => FleshPaint(new Vector3(t, -t * 2f, 0), AFlesh.Lerp(AFleshD, t * 0.5f)));
        m.Use(AlienArmor());
        // Épaulière : trois lames cintrées qui se chevauchent, de plus en plus inclinées vers l'extérieur.
        var g = guard ? 1.25f : 1f;
        for (var l = 0; l < 3; l++)
        {
            var tilt = new Basis(Vector3.Back, -(0.35f + l * 0.3f));
            var c = new Vector3(0.09f + l * 0.07f, 0.1f - l * 0.12f, 0) * g;
            var R = (0.26f - l * 0.02f) * g;
            var rings = new System.Collections.Generic.List<InsectMesh.Ring>();
            const int n = 9;
            for (var i = 0; i < n; i++)
            {
                var a = -1.3f + i * 2.6f / (n - 1);
                var p = c + tilt * new Vector3(0, Mathf.Cos(a) * R * 0.9f, Mathf.Sin(a) * R * 1.15f);
                var nn = tilt * new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
                var w = (0.21f - l * 0.025f) * g * (1f - 0.3f * Mathf.Abs(a) / 1.3f);
                var col = l == 1 ? ACarap2 : ACarap;
                if (i == 0) rings.Add(new InsectMesh.Ring(p, tilt * Vector3.Right, nn, 0.001f, 0.001f, col));
                rings.Add(new InsectMesh.Ring(p, tilt * Vector3.Right, nn, w, 0.035f * g, col));
                if (i == n - 1) rings.Add(new InsectMesh.Ring(p, tilt * Vector3.Right, nn, 0.001f, 0.001f, col));
            }
            m.Loft(rings, 10, 4f);
        }
        if (guard)
            for (var k = 0; k < 3; k++)
                m.Cone(new Vector3(0.12f + k * 0.04f, 0.22f - k * 0.04f, -0.15f + k * 0.15f), new Vector3(0.32f + k * 0.05f, 0.5f - k * 0.05f, -0.2f + k * 0.18f), 0.045f, 6, ATrim, true);
        // Brassière : coques externe, avant et arrière ; la chair ne paraît que dans les joints.
        Shell(m, new Vector3(0.12f, -0.5f, 0), new Vector3(1, 0, 0), Vector3.Down, new Vector3(0.15f, 0.1f, 0.32f), ACarap);
        Shell(m, new Vector3(0.02f, -0.55f, 0.13f), new Vector3(0.2f, 0, 1), Vector3.Down, new Vector3(0.13f, 0.08f, 0.27f), ACarap2);
        Shell(m, new Vector3(0.02f, -0.5f, -0.13f), new Vector3(0.2f, 0, -1), Vector3.Down, new Vector3(0.13f, 0.08f, 0.28f), ACarap);
        m.Use(MachMech());
        m.Ellipsoid(new Vector3(0, -1.0f, 0), new Vector3(0.12f, 0.12f, 0.12f), 4, 10, ATrim);
        // Brassards sombres sur la chair du bras.
        Cyl(m, new Vector3(0, -0.36f, 0), new Vector3(0, -0.44f, 0), 0.215f, 12, ATrim);
        Cyl(m, new Vector3(0, -0.72f, 0), new Vector3(0, -0.79f, 0), 0.185f, 12, ATrim);
    }

    private static void AlienForearm(InsectMesh m, bool guard)
    {
        m.Use(AlienFlesh());
        m.Segment(new Vector3(0, 0, 0), new Vector3(0, -0.9f, 0), 0.13f, 0.09f, 10, AFleshD, Vector3.Back, 0f, 3,
            t => 1f + 0.12f * Mathf.Sin(t * Mathf.Pi));
        m.Use(AlienArmor());
        // Canon d'avant-bras sur la face externe et le dessus, épine de coude.
        Shell(m, new Vector3(0.06f, -0.45f, 0), new Vector3(1, 0, 0), Vector3.Down, new Vector3(0.12f, 0.08f, 0.42f), ACarap);
        Shell(m, new Vector3(0.0f, -0.42f, -0.06f), new Vector3(0, 0, -1), Vector3.Down, new Vector3(0.1f, 0.07f, 0.38f), ACarap2);
        if (guard) Shell(m, new Vector3(0.0f, -0.4f, 0.07f), new Vector3(0, 0, 1), Vector3.Down, new Vector3(0.1f, 0.07f, 0.36f), ACarap);
        m.Use(MachMech());
        m.Cone(new Vector3(0, -0.02f, -0.1f), new Vector3(0, 0.12f, -0.34f), 0.05f, 6, ABone, true);
        Cyl(m, new Vector3(0, -0.84f, 0), new Vector3(0, -0.95f, 0), 0.105f, 10, ATrim);
    }

    private static void AlienHand(InsectMesh m)
    {
        m.Use(AlienArmor());
        // Dos de la main (paume vers +Z), trois longs doigts recourbés, pouce opposable, griffes.
        Plate(m, new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), new Vector3(0, -0.12f, 0)), new Vector3(0.11f, 0.045f, 0.13f), ACarap2, 0.03f, 6f);
        m.Use(AlienFlesh());
        for (var k = -1; k <= 1; k++)
        {
            var x = k * 0.07f;
            m.Tube(new[] { new Vector3(x, -0.22f, 0.0f), new Vector3(x, -0.34f, 0.04f), new Vector3(x, -0.39f, 0.13f), new Vector3(x, -0.35f, 0.21f) },
                new[] { 0.035f, 0.032f, 0.028f, 0.022f }, 6, AFleshD, Vector3.Right);
        }
        m.Tube(new[] { new Vector3(0.1f, -0.06f, 0.04f), new Vector3(0.13f, -0.15f, 0.13f), new Vector3(0.09f, -0.22f, 0.2f) }, new[] { 0.04f, 0.035f, 0.028f }, 6, AFleshD, Vector3.Right);
        m.Use(MachMech());
        for (var k = -1; k <= 1; k++)
            m.Cone(new Vector3(k * 0.07f, -0.35f, 0.21f), new Vector3(k * 0.07f, -0.27f, 0.27f), 0.022f, 5, ABone);
    }

    /// <summary>Fusil à énergie (≈ 2,7 m ; carabine du volant ≈ 2,1 m) : culasse, manchon à bobines incandescentes, cellule latérale.</summary>
    private static void AlienRifle(InsectMesh m, bool carbine)
    {
        var z1 = carbine ? 1.3f : 1.78f;
        m.Use(AlienArmor());
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.02f, 0.1f)), new Vector3(0.11f, 0.15f, 0.48f), ACarap2, 0.04f, 7f, 0.9f);
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.17f, 0.12f)), new Vector3(0.05f, 0.04f, 0.4f), ATrim, 0.015f, 6f);
        // Crosse squelettique.
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.0f, -0.55f)), new Vector3(0.055f, 0.1f, 0.24f), ACarap, 0.03f, 6f, 1f, 1.3f);
        m.Use(MachMech());
        Cyl(m, new Vector3(0, 0.04f, 0.5f), new Vector3(0, 0.04f, z1), 0.075f, 10, ATrim);
        Cyl(m, new Vector3(0, -0.07f, 0.5f), new Vector3(0, -0.07f, z1 - 0.35f), 0.04f, 8, ATrim);
        // Poignée pistolet (inclinée en arrière) et poignée avant.
        HangPlate(m, new Vector3(0, -0.2f, -0.14f), new Vector3(0, 0, 1), new Vector3(0.045f, 0.05f, 0.15f), ATrim, 0.02f, 0.9f, -0.35f);
        HangPlate(m, new Vector3(0, -0.15f, carbine ? 0.55f : 0.78f), new Vector3(0, 0, 1), new Vector3(0.04f, 0.04f, 0.11f), ATrim, 0.02f, 0.9f);
        // Fourche d'émission à la bouche.
        for (var s = -1; s <= 1; s += 2)
            Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.04f + s * 0.1f, z1 + 0.06f)), new Vector3(0.05f, 0.025f, 0.17f), ACarap2, 0.015f, 6f, 0.5f);
        m.Use(AlienGlow());
        var n = carbine ? 3 : 5;
        for (var k = 0; k < n; k++)
            m.Torus(new Vector3(0, 0.04f, 0.72f + k * 0.2f), new Basis(Vector3.Right, Mathf.Pi / 2), 0.09f, 0.022f, 14, 4, Colors.White);
        Cyl(m, new Vector3(0.13f, 0.0f, -0.04f), new Vector3(0.13f, 0.0f, 0.34f), 0.05f, 10, Colors.White);
        m.Ellipsoid(new Vector3(0, 0.04f, z1 + 0.02f), new Vector3(0.05f, 0.05f, 0.02f), 2, 8, Colors.White);
        m.Use(MachMech());
        for (var k = 0; k < 3; k++)
            Cyl(m, new Vector3(0.13f, 0.0f, -0.02f + k * 0.15f), new Vector3(0.13f, 0.0f, 0.04f + k * 0.15f), 0.062f, 10, ATrim);
    }

    /// <summary>Blaster lourd du garde (tenu d'une main, à la hanche) : caisson massif, canon court à trois bobines, tambour.</summary>
    private static void AlienBlaster(InsectMesh m)
    {
        m.Use(AlienArmor());
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.03f, 0.15f)), new Vector3(0.15f, 0.19f, 0.42f), ACarap2, 0.05f, 6f, 0.85f);
        m.Use(MachMech());
        Cyl(m, new Vector3(0, 0.03f, 0.5f), new Vector3(0, 0.03f, 1.05f), 0.11f, 12, ATrim, -1f, 0.09f);
        Cyl(m, new Vector3(0.0f, 0.03f, -0.35f), new Vector3(0.0f, 0.03f, -0.15f), 0.2f, 14, ATrim);
        HangPlate(m, new Vector3(0, -0.2f, -0.05f), new Vector3(0, 0, 1), new Vector3(0.05f, 0.05f, 0.14f), ATrim, 0.02f, 0.9f, -0.25f);
        m.Use(AlienGlow());
        for (var k = 0; k < 3; k++)
            m.Torus(new Vector3(0, 0.03f, 0.62f + k * 0.15f), new Basis(Vector3.Right, Mathf.Pi / 2), 0.13f, 0.028f, 14, 4, Colors.White);
        m.Ellipsoid(new Vector3(0, 0.03f, 1.06f), new Vector3(0.08f, 0.08f, 0.02f), 2, 8, Colors.White);
    }

    /// <summary>Bouclier de tour du garde (≈ 1,8 × 3 m) : cinq lames cintrées, nervure, bossettes, liseré d'énergie.</summary>
    private static void AlienShieldMesh(InsectMesh m)
    {
        m.Use(AlienArmor());
        const float R = 1.7f, H = 1.5f;
        for (var k = -2; k <= 2; k++)
        {
            var a = k * 0.21f;
            var c = new Vector3(Mathf.Sin(a) * R, 0, Mathf.Cos(a) * R - R);
            var b = new Basis(Vector3.Up, a);
            var top = k == 0 ? H + 0.15f : H - Mathf.Abs(k) * 0.08f;
            Plate(m, new Transform3D(b * new Basis(Vector3.Right, Mathf.Pi / 2), c + new Vector3(0, (top - H) * 0.5f, 0)),
                new Vector3(0.19f, 0.06f, top), k % 2 == 0 ? ACarap : ACarap * 0.8f, 0.04f, 5f, 0.85f);
            // Écailles de renfort en chevrons sur chaque lame.
            for (var j = -2; j <= 2; j++)
                Plate(m, new Transform3D(b * new Basis(Vector3.Right, Mathf.Pi / 2 - 0.25f), c + new Vector3(0, j * 0.55f, 0.06f)),
                    new Vector3(0.17f, 0.03f, 0.16f), ACarap2, 0.025f, 5f, 0.6f);
        }
        // Rebord épais et cornes au sommet.
        for (var s = -1; s <= 1; s += 2)
        {
            var a = s * 0.47f;
            Plate(m, new Transform3D(new Basis(Vector3.Up, a) * new Basis(Vector3.Right, Mathf.Pi / 2), new Vector3(Mathf.Sin(a) * R, -0.05f, Mathf.Cos(a) * R - R + 0.02f)),
                new Vector3(0.06f, 0.1f, H + 0.05f), ACarap2, 0.03f, 5f, 0.95f);
        }
        m.Use(MachMech());
        Plate(m, new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), new Vector3(0, 0.05f, 0.1f)), new Vector3(0.08f, 0.08f, H + 0.15f), ATrim, 0.03f, 5f, 0.7f);
        m.Ellipsoid(new Vector3(0, 0.35f, 0.14f), new Vector3(0.22f, 0.26f, 0.12f), 5, 12, ACarap2);
        for (var s = -1; s <= 1; s += 2)
            m.Cone(new Vector3(Mathf.Sin(s * 0.36f) * R, H - 0.1f, Mathf.Cos(0.36f) * R - R), new Vector3(Mathf.Sin(s * 0.5f) * R, H + 0.55f, Mathf.Cos(0.5f) * R - R + 0.15f), 0.08f, 6, ABone, true);
        for (var y = -1; y <= 1; y++)
        for (var s = -1; s <= 1; s += 2)
            m.Ellipsoid(new Vector3(Mathf.Sin(s * 0.42f) * R, y * 0.95f, Mathf.Cos(0.42f) * R - R + 0.07f), new Vector3(0.06f, 0.06f, 0.04f), 3, 8, ATrim);
        // Poignée intérieure.
        Cyl(m, new Vector3(-0.12f, 0.1f, -0.3f), new Vector3(0.12f, 0.1f, -0.3f), 0.05f, 8, ATrim);
        Cyl(m, new Vector3(0, -0.2f, -0.08f), new Vector3(0, 0.4f, -0.08f), 0.06f, 8, ATrim);
        m.Use(AlienGlow());
        for (var s = -1; s <= 1; s += 2)
        {
            var a = s * 0.5f;
            m.Box(new Transform3D(new Basis(Vector3.Up, a), new Vector3(Mathf.Sin(a) * (R + 0.01f), -0.05f, Mathf.Cos(a) * (R + 0.01f) - R)), new Vector3(0.015f, H - 0.25f, 0.025f), Colors.White);
        }
        // Émetteur central : fente violette dans la bosse.
        m.Box(new Transform3D(Basis.Identity, new Vector3(0, 0.35f, 0.26f)), new Vector3(0.025f, 0.18f, 0.012f), Colors.White);
        // Champ d'énergie : voile additif à peine visible devant les lames.
        m.Use(AlienShieldFilm());
        m.Ellipsoid(new Vector3(0, 0.05f, 0.05f), new Vector3(1.05f, H + 0.25f, 0.06f), 8, 20, new Color(1f, 1f, 1f, 1f));
    }

    private static StandardMaterial3D _alFilm;

    /// <summary>Voile du bouclier : additif, violet très léger, plus marqué sur les bords (liseré).</summary>
    private static StandardMaterial3D AlienShieldFilm() => _alFilm ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(AlienEnergy, 0.07f),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        RimEnabled = true,
        Rim = 1f,
    };
}
