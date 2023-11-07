// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Skinning;

namespace osu.Game.Screens.Play.HUD
{
    public partial class ArgonScoreCounter : GameplayScoreCounter, ISerialisableDrawable
    {
        [SettingSource("Wireframe opacity", "Controls the opacity of the wire frames behind the digits.")]
        public BindableFloat WireframeOpacity { get; } = new BindableFloat(0.4f)
        {
            Precision = 0.01f,
            MinValue = 0,
            MaxValue = 1,
        };

        public bool UsesFixedAnchor { get; set; }

        protected override LocalisableString FormatCount(long count) => count.ToLocalisableString();

        protected override IHasText CreateText() => new ArgonScoreTextComponent(Anchor.TopRight)
        {
            RequiredDisplayDigits = { BindTarget = RequiredDisplayDigits },
            WireframeOpacity = { BindTarget = WireframeOpacity },
        };

        private partial class ArgonScoreTextComponent : ArgonCounterTextComponent
        {
            public IBindable<int> RequiredDisplayDigits { get; } = new BindableInt();

            public ArgonScoreTextComponent(Anchor anchor, LocalisableString? label = null)
                : base(anchor, label)
            {
            }

            protected override LocalisableString FormatWireframes(LocalisableString text) => new string('#', Math.Max(text.ToString().Length, RequiredDisplayDigits.Value));
        }
    }
}
