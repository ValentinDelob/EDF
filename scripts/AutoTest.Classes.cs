using System;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Tests des classes (arguments après « -- ») :
/// --heavy-sets : Grenadier blindé, trois bascules de jeu d'armes par la vraie touche (action « weapon_set ») ; vérifie les
/// quatre armes, le blocage de 0,5 s (arme abaissée, aucun tir) puis la reprise ; journal « [jeux] … », « [jeux] ok » à la fin.
/// --call-vehicle=tank|heli : Opérateur d'appui, crédits fournis, appel du véhicule devant la caméra, puis second appel de l'autre
/// type (l'ancien véhicule libre est rapatrié) et montée à bord ; journal « [appel] … », « [appel] ok » à la fin.
/// Avec --shots=dossier, captures aux moments clés (préfixes heavy_sets_*, call_*).
/// </summary>
public static partial class AutoTest
{
    public static void ClassTests(App app)
    {
        if (Screenshot.CmdArg("heavy-sets") != null) WhenPlayerReady(app, 3f, p => HeavySetsTest(app, p));
        if (Screenshot.CmdArg("call-vehicle") is { } cv)
        {
            World.VehicleCallLog = true;
            var first = cv.StartsWith("heli") ? CallIns.Heli : CallIns.Tank;
            WhenPlayerReady(app, 3f, p =>
            {
                if (Screenshot.CmdArg("shots") == null) { CallVehicleTest(app, p, first); return; }
                // Captures : le bot vise d'abord le sol à ~35 m devant lui (largage proche, bien cadré).
                var f = p.AimForward;
                f.Y = 0;
                f = f.LengthSquared() > 0.01f ? f.Normalized() : Vector3.Forward;
                Player.BotLookAt = p.GlobalPosition + f * 35f;
                At(app, 1.5f, () => CallVehicleTest(app, p, first));
            });
        }
    }

    /// <summary>Attend que la mission soit chargée (soldat local présent), puis lance l'action après <paramref name="delay"/> s.</summary>
    private static void WhenPlayerReady(App app, float delay, Action<Player> then)
    {
        app.GetTree().CreateTimer(1f).Timeout += () =>
        {
            var w = app.CurrentWorld;
            if (w?.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer)) { WhenPlayerReady(app, delay, then); return; }
            app.GetTree().CreateTimer(delay).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(w.LocalPlayer)) then(w.LocalPlayer);
            };
        };
    }

    private static void At(App app, float s, Action a) => app.GetTree().CreateTimer(s).Timeout += a;

    private static void Shot(App app, string name)
    {
        if (Screenshot.CmdArg("shots") != null) Screenshot.Take(app.GetViewport(), name);
    }

    private static void PressAction(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
    }

    private static void HeavySetsTest(App app, Player p)
    {
        var errors = 0;
        void Fail(string msg) { errors++; GD.PrintErr($"ERROR: [jeux] {msg}"); }
        if (p.Def.Id != SoldierClass.Heavy) { Fail("le test demande le Grenadier blindé (--class=3)"); return; }
        if (p.Weapons.Count != 4 || p.Weapons.Select(w => w.W.TemplateId).Distinct().Count() != 4)
            Fail($"4 armes distinctes attendues, {p.Weapons.Count} équipées");
        if (!p.HasSets || p.SetCount != 2) Fail("deux jeux d'armes attendus");
        GD.Print($"[jeux] armes : {string.Join(" | ", p.Weapons.Select((w, i) => $"{i + 1}:{w.W.T.Name}"))}");
        Shot(app, "heavy_sets_start");
        for (var k = 0; k < 3; k++)
        {
            var t0 = 1f + k * 3f; // la capture de départ (à 0 s) fige une image : on la laisse passer
            var firstSwap = k == 0;
            // Les événements injectés sont traités à l'image suivante : chaque vérification attend 0,1 s.
            var before = -1;
            var weaponBefore = -1;
            At(app, t0, () =>
            {
                before = p.ActiveSet;
                PressAction("weapon_set");
            });
            At(app, t0 + 0.1f, () =>
            {
                if (p.ActiveSet == before) Fail($"le jeu n'a pas changé (jeu {before + 1})");
                if (p.SetSwapLeft <= 0) Fail("aucun blocage après la bascule");
                if (!p.Model.Reloading) Fail("arme non abaissée pendant la bascule");
                // Une seconde bascule pendant le blocage doit être ignorée.
                before = p.ActiveSet;
                PressAction("weapon_set");
            });
            At(app, t0 + 0.2f, () =>
            {
                if (p.ActiveSet != before) Fail("bascule acceptée pendant le blocage");
                if (p.SetSwapLeft <= 0) Fail("blocage terminé trop tôt");
                if (firstSwap) Shot(app, "heavy_sets_swap");
            });
            At(app, t0 + 0.75f, () =>
            {
                if (p.SetSwapLeft > 0) Fail($"blocage encore actif après 0,75 s ({p.SetSwapLeft:0.00} s)");
                // Arme suivante : change d'arme sans quitter le jeu actif.
                before = p.ActiveSet;
                weaponBefore = p.Current;
                PressAction("weapon_swap");
            });
            At(app, t0 + 0.85f, () =>
            {
                if (p.ActiveSet != before) Fail("« arme suivante » a quitté le jeu actif");
                if (p.Current == weaponBefore) Fail("« arme suivante » sans effet");
                GD.Print($"[jeux] jeu {p.ActiveSet + 1} prêt, arme {p.Current + 1} : {p.Weapon.W.T.Name}");
            });
        }
        At(app, 3.5f, () => Shot(app, "heavy_sets_hud"));
        At(app, 11f, () =>
        {
            if (errors == 0) GD.Print("[jeux] ok : 3 bascules de 0,5 s, 4 armes en deux jeux");
            Shot(app, "heavy_sets_end");
        });
    }

    private static void CallVehicleTest(App app, Player p, int first)
    {
        var errors = 0;
        void Fail(string msg) { errors++; GD.PrintErr($"ERROR: [appel] {msg}"); }
        // Autre classe (ex. hôte d'un test LAN) : journal des livraisons seulement.
        if (p.Def.Id != SoldierClass.Support) { GD.Print("[appel] journal seul (classe non Opérateur)"); return; }
        var w = p.World;
        int firstId = 0, secondId = 0;
        p.CallChoice = first;
        p.CallCredits = CallIns.Costs[first] + 50f;
        if (!p.TryCallVehicle()) Fail("appel refusé localement");
        At(app, 0.5f, () =>
        {
            firstId = p.CalledVehicleId;
            if (firstId == 0) Fail("aucune confirmation de l'hôte");
            else if (p.CallCredits > 90f) Fail($"crédits non débités ({p.CallCredits:0})");
            GD.Print($"[appel] confirmé : n°{firstId}, crédits restants {p.CallCredits:0}");
        });
        // Captures : le bot suit la livraison des yeux (avion, puis véhicule sous voilure).
        if (Screenshot.CmdArg("shots") != null)
            for (var t = 0.6f; t < 10f; t += 0.2f)
                At(app, t, () =>
                {
                    if (!w.Vehicles.TryGetValue(p.CalledVehicleId, out var v)) return;
                    Player.BotLookAt = v.Visible ? v.GlobalPosition + Vector3.Up * 3f : v.Home + Vector3.Up * 45f;
                });
        At(app, 10f, () => Player.BotLookAt = null);
        At(app, 1.7f, () => Shot(app, "call_plane"));
        At(app, 2.8f, () => Shot(app, "call_chutes"));
        At(app, 4.8f, () => Shot(app, "call_drop"));
        At(app, 7f, () => Shot(app, "call_retro"));
        At(app, 8.6f, () => Shot(app, "call_touchdown"));
        At(app, 10f, () =>
        {
            Shot(app, "call_landed");
            if (!w.Vehicles.TryGetValue(firstId, out var v)) { Fail("véhicule introuvable"); return; }
            if (v.Delivering) Fail("livraison toujours en cours après 10 s");
            if (v.CalledBy != Net.I.MyId) Fail("demandeur incorrect");
            GD.Print($"[appel] n°{firstId} {v.DisplayName} posé à {v.GlobalPosition.Snapped(Vector3.One * 0.1f)}, à {v.GlobalPosition.DistanceTo(p.GlobalPosition):0} m");
            // Second appel (autre type) : l'ancien, libre, est rapatrié.
            p.CallChoice = (first + 1) % CallIns.Count;
            p.CallCredits = CallIns.Costs[p.CallChoice];
            if (!p.TryCallVehicle()) Fail("second appel refusé localement");
        });
        At(app, 20f, () =>
        {
            secondId = p.CalledVehicleId;
            if (w.Vehicles.ContainsKey(firstId)) Fail("l'ancien véhicule n'a pas été rapatrié");
            if (secondId == firstId || !w.Vehicles.TryGetValue(secondId, out var v2)) { Fail("second véhicule introuvable"); return; }
            if (v2.Delivering) Fail("second véhicule encore en livraison");
            GD.Print($"[appel] remplacement : n°{firstId} rapatrié, n°{secondId} {v2.DisplayName} posé");
            p.GlobalPosition = v2.GlobalPosition + v2.GlobalBasis.X * 4f + Vector3.Up;
            Net.I.RequestVehicleSeat(secondId, true);
        });
        At(app, 21.5f, () =>
        {
            if (p.Vehicle == null || p.Vehicle.Id != secondId) Fail("montée à bord impossible");
            else GD.Print($"[appel] à bord du n°{secondId}");
            // Occupé : un nouvel appel est refusé.
            p.CallCredits = CallIns.Cap;
            if (p.CalledVehicleBusy == false) Fail("véhicule occupé non détecté");
            Shot(app, "call_aboard");
            if (errors == 0) GD.Print("[appel] ok : largage, débit des crédits, remplacement, montée à bord");
        });
    }
}
