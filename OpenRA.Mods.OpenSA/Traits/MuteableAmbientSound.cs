#region Copyright & License Information
/*
 * Copyright 2019-2021 The OpenSA Developers (see CREDITS)
 * This file is part of OpenSA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenSA.Traits.Sound
{
	[Desc("Plays a looping audio file at the actor position. Attach this to the `World` actor to cover the whole map.")]
	class MuteableAmbientSoundInfo : ConditionalTraitInfo
	{
		[FieldLoader.Require]
		public readonly string[] SoundFiles = null;

		[Desc("Initial delay (in ticks) before playing the sound for the first time.",
			"Two values indicate a random delay range.")]
		public readonly int[] Delay = { 0 };

		[Desc("Interval between playing the sound (in ticks).",
			"Two values indicate a random delay range.")]
		public readonly int[] Interval = { 0 };

		public override object Create(ActorInitializer init) { return new MuteableAmbientSound(init.Self, this); }
	}

	class MuteableAmbientSound : ConditionalTrait<MuteableAmbientSoundInfo>, ITick, INotifyRemovedFromWorld
	{
		readonly bool loop;
		readonly HashSet<ISound> currentSounds = new HashSet<ISound>();
		WPos cachedPosition;
		int delay;
		bool wasMuted;

		public MuteableAmbientSound(Actor self, MuteableAmbientSoundInfo info)
			: base(info)
		{
			delay = Util.RandomInRange(self.World.LocalRandom, info.Delay);
			loop = Info.Interval.Length == 0 || (Info.Interval.Length == 1 && Info.Interval[0] == 0);
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled)
				return;

			if (Game.Settings.Sound.MuteBackgroundMusic)
			{
				StopSound();
				wasMuted = true;
				return;
			}

			if (wasMuted && !Game.Settings.Sound.MuteBackgroundMusic)
			{
				delay = Util.RandomInRange(self.World.LocalRandom, Info.Delay);
				wasMuted = false;
			}

			currentSounds.RemoveWhere(s => s == null || s.Complete);

			if (self.OccupiesSpace != null)
			{
				var pos = self.CenterPosition;
				if (pos != cachedPosition)
				{
					foreach (var s in currentSounds)
						s.SetPosition(pos);

					cachedPosition = pos;
				}
			}

			if (delay < 0)
				return;

			if (--delay < 0)
			{
				StartSound(self);
				if (!loop)
					delay = Util.RandomInRange(self.World.LocalRandom, Info.Interval);
			}
		}

		void StartSound(Actor self)
		{
			var sound = Info.SoundFiles.RandomOrDefault(Game.CosmeticRandom);

			ISound s;
			if (self.OccupiesSpace != null)
			{
				cachedPosition = self.CenterPosition;
				s = loop ? Game.Sound.PlayLooped(SoundType.World, sound, cachedPosition) :
					Game.Sound.Play(SoundType.World, sound, self.CenterPosition);
			}
			else
				s = loop ? Game.Sound.PlayLooped(SoundType.World, sound) :
					Game.Sound.Play(SoundType.World, sound);

			currentSounds.Add(s);
		}

		void StopSound()
		{
			foreach (var s in currentSounds)
				Game.Sound.StopSound(s);

			currentSounds.Clear();
		}

		protected override void TraitEnabled(Actor self) { delay = Util.RandomInRange(self.World.LocalRandom, Info.Delay); }
		protected override void TraitDisabled(Actor self) { StopSound(); }

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self) { StopSound(); }
	}
}
