using System;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;

namespace TempestBuffPlugin
{
    [ApiVersion(2, 1)]
    public class TempestBuff : TerrariaPlugin
    {
        public override string Name => "TempestBuff";
        public override Version Version => new Version(1, 1, 0);
        public override Author => "CustomPlugin";
        public override string Description => "Buff Tempest Staff yang kecepatannya bisa diatur langsung lewat command.";

        private int updateCounter = 0;

        private float speedMultiplier = 1.5f;
        private float homingStrength = 0.07f;

        public TempestBuff(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.ProjectileSetDefaults.Register(this, OnProjectileSetDefaults);
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

            Commands.ChatCommands.Add(new Command("tempest.buff", TempestCommand, "tempestbuff", "tbuff")
            {
                HelpText = "Gunakan /tbuff [speed <angka> | homing <angka>]"
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ProjectileSetDefaults.Deregister(this, OnProjectileSetDefaults);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            }
            base.Dispose(disposing);
        }

        private void TempestCommand(CommandArgs args)
        {
            if (args.Parameters.Count == 0)
            {
                args.Player.SendInfoMessage($"[Tempest Buff] Speed: {speedMultiplier}x | Homing: {homingStrength} | Ukuran: -10% (0.9x).");
                args.Player.SendInfoMessage("Cara ubah: /tbuff speed <angka> (contoh: 1.5) atau /tbuff homing <angka> (contoh: 0.07)");
                return;
            }

            string sub = args.Parameters[0].ToLower();

            if (sub == "speed" && args.Parameters.Count >= 2 && float.TryParse(args.Parameters[1], out float newSpeed))
            {
                if (newSpeed < 0.5f || newSpeed > 5.0f)
                {
                    args.Player.SendErrorMessage("[Tempest Buff] Kecepatan harus antara 0.5 sampai 5.0!");
                    return;
                }

                speedMultiplier = newSpeed;
                args.Player.SendSuccessMessage($"[Tempest Buff] Kecepatan proyektil diatur ke {speedMultiplier}x!");
            }
            else if (sub == "homing" && args.Parameters.Count >= 2 && float.TryParse(args.Parameters[1], out float newHoming))
            {
                if (newHoming < 0f || newHoming > 0.5f)
                {
                    args.Player.SendErrorMessage("[Tempest Buff] Kekuatan homing harus antara 0 (mati) sampai 0.5!");
                    return;
                }

                homingStrength = newHoming;
                args.Player.SendSuccessMessage($"[Tempest Buff] Kekuatan homing diatur ke {homingStrength}!");
            }
            else
            {
                args.Player.SendErrorMessage("Format salah! Gunakan: /tbuff, /tbuff speed <angka>, atau /tbuff homing <angka>");
            }
        }

        private void OnProjectileSetDefaults(SetDefaultsEventArgs<Projectile, int> args)
        {
            Projectile proj = args.Object;
            if (proj == null)
                return;

            if (proj.type == ProjectileID.Tempest || proj.type == ProjectileID.MiniSharkron)
            {
                proj.scale *= 0.9f;
                proj.width = (int)Math.Max(4, Math.Round(proj.width * 0.9f));
                proj.height = (int)Math.Max(4, Math.Round(proj.height * 0.9f));

                if (proj.type == ProjectileID.Tempest)
                {
                    proj.extraUpdates = 1;
                }
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            updateCounter++;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj == null || !proj.active)
                    continue;

                if (proj.owner < 0 || proj.owner >= Main.maxPlayers)
                    continue;

                TSPlayer tsPlayer = TShock.Players[proj.owner];
                if (tsPlayer == null || !tsPlayer.Active || !tsPlayer.HasPermission("tempest.buff"))
                    continue;

                if (proj.type == ProjectileID.MiniSharkron)
                {
                    if (proj.timeLeft >= 358 && proj.localAI[1] == 0f)
                    {
                        proj.velocity *= speedMultiplier;
                        proj.localAI[1] = 1f;
                        NetMessage.SendData((int)PacketTypes.ProjectileNew, -1, -1, null, proj.whoAmI);
                    }

                    if (homingStrength > 0f && updateCounter % 3 == 0)
                    {
                        ApplySoftHoming(proj);
                    }
                }
            }
        }

        private void ApplySoftHoming(Projectile proj)
        {
            float maxDetectDistance = 400f;
            NPC closestTarget = null;
            float closestDist = maxDetectDistance;

            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];
                if (npc != null && npc.active && !npc.friendly && !npc.dontTakeDamage && npc.lifeMax > 5 && npc.CanBeChasedBy(proj))
                {
                    float dist = Vector2.Distance(proj.Center, npc.Center);
                    if (dist < closestDist)
                    {
                        if (Collision.CanHitLine(proj.position, proj.width, proj.height, npc.position, npc.width, npc.height))
                        {
                            closestDist = dist;
                            closestTarget = npc;
                        }
                    }
                }
            }

            if (closestTarget != null)
            {
                float currentSpeed = proj.velocity.Length();
                if (currentSpeed <= 0.1f)
                    return;

                Vector2 desiredDirection = closestTarget.Center - proj.Center;
                desiredDirection.Normalize();
                Vector2 desiredVelocity = desiredDirection * currentSpeed;

                proj.velocity = Vector2.Lerp(proj.velocity, desiredVelocity, homingStrength);
                proj.velocity.Normalize();
                proj.velocity *= currentSpeed;

                NetMessage.SendData((int)PacketTypes.ProjectileNew, -1, -1, null, proj.whoAmI);
            }
        }
    }
}
