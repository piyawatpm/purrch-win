using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace Purrch
{
    /// Plays the embedded WAV cues (meow / bark / crunch), matching the macOS
    /// build. SoundPlayer has no volume control, so this is a simple on/off; each
    /// clip is loaded into memory once and reused.
    public class Sounds
    {
        public bool Enabled = true;

        private readonly SpriteLibrary lib;
        private readonly Dictionary<string, SoundPlayer> players = new();

        public Sounds(SpriteLibrary lib) { this.lib = lib; }

        public void Play(string name)
        {
            if (!Enabled) return;
            try
            {
                if (!players.TryGetValue(name, out var player))
                {
                    using var raw = lib.ResourceStream(name + ".wav");
                    if (raw == null) return;
                    var mem = new MemoryStream();
                    raw.CopyTo(mem);
                    mem.Position = 0;
                    player = new SoundPlayer(mem);
                    player.Load();
                    players[name] = player;
                }
                player.Play();   // asynchronous
            }
            catch { /* audio is non-essential */ }
        }
    }
}
