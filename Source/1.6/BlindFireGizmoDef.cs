using UnityEngine;
using RimWorld;
using Verse;

namespace EyesOnTheBackstop
{
    public class BlindFireGizmoDef : Def
    {
        public string iconPath;

        private Texture2D cachedIcon;

        public Texture2D CommandIcon
        {
            get
            {
                if (cachedIcon == null)
                {
                    cachedIcon = iconPath.NullOrEmpty()
                        ? TexCommand.Attack
                        : ContentFinder<Texture2D>.Get(iconPath, reportFailure: false) ?? TexCommand.Attack;
                }

                return cachedIcon;
            }
        }
    }
}
