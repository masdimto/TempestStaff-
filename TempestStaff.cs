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
        public override Version Version => new Version(1, 0, 0);
        public override Author => "CustomPlugin";
        public override string Description => "Buff Tempest Staff: Ukuran -10%, Kecepatan 1.5x, dan Soft Homing seimbang.";

        private int updateCounter = 0;

        public TempestBuff(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.ProjectileSetDefaults.Register(this, OnProjectileSetDefaults);
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

            Commands.ChatCommands.Add(new Command("tempest.buff", TempestInfoCommand, "tempestbuff", "tbuff")
            {
                HelpText = "Melihat status buff Tempest Staff."
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

        private void TempestInfoCommand(CommandArgs args)
        {
            args.Player.SendInfoMessage("[Tempest Buff] Aktif! Ukuran minion & proyektil -10% (0.9x), kecepatan 1.5x, dan proyektil memiliki soft homing.");
        }

        private void OnProjectileSetDefaults(SetDefaultsEventArgs<Projectile, int> args)
        {
            Projectile proj = args.Object;
            if (proj == null)
                return;

            // 1. Minion Sharknado (ProjectileID.Tempest) & Proyektil Hiu Kecil (ProjectileID.MiniSharkron)
            if (proj.type == ProjectileID.Tempest || proj.type == ProjectileID.MiniSharkron)
            {
                // Ukuran 10% lebih kecil (0.9x)
                proj.scale *= 0.9f;
                proj.width = (int)Math.Max(4, Math.Round(proj.width * 0.9f));
                proj.height = (int)Math.Max(4, Math.Round(proj.height * 0.9f));

                // Kecepatan gerak minion Sharknado 1.5x lebih cepat
                if (proj.type == ProjectileID.Tempest)
                {
                    proj.extraUpdates = 1; // Membantu pergerakan minion lebih responsif
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

                // Terapkan pada proyektil tembakan hiu kecil dari Tempest Staff
                if (proj.type == ProjectileID.MiniSharkron)
                {
                    // Pada awal kemunculan proyektil (timeLeft masih penuh), tingkatkan kecepatan 1.5x
                    // MiniSharkron default timeLeft adalah 360
                    if (proj.timeLeft >= 358 && proj.localAI[1] == 0f)
                    {
                        proj.velocity *= 1.5f;
                        proj.localAI[1] = 1f; // Tandai agar tidak dikali 1.5x berulang kali
                        NetMessage.SendData((int)PacketTypes.ProjectileNew, -1, -1, null, proj.whoAmI);
                    }

                    // Soft Homing setiap 3 tick agar halus, ringan untuk server, dan tidak terlalu OP
                    if (updateCounter % 3 == 0)
                    {
                        ApplySoftHoming(proj);
                    }
                }
            }
        }

        private void ApplySoftHoming(Projectile proj)
        {
            float maxDetectDistance = 400f; // ~25 blok jarak deteksi
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
                        // Pastikan musuh masih dalam jangkauan pandang (tidak menembus tembok tebal)
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

                // Faktor belok lembut (0.07f = ~7% belokan tiap update, cukup untuk melengkung tapi tidak auto-lock 100%)
                float turnStrength = 0.07f;
                proj.velocity = Vector2.Lerp(proj.velocity, desiredVelocity, turnStrength);

                // Jaga kecepatan tetap konsisten di 1.5x
                proj.velocity.Normalize();
                proj.velocity *= currentSpeed;

                NetMessage.SendData((int)PacketTypes.ProjectileNew, -1, -1, null, proj.whoAmI);
            }
        }
    }
}
