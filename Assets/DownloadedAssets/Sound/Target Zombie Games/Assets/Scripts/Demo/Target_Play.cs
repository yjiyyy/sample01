// Target_Play : Description : A simple script to play sounds
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TS.ZombieGames
{
	public class Target_Play : MonoBehaviour
	{
		private AudioSource s_Audio;                // Access audioSource component
		[Header("Put your samples here")]
		public AudioClip[] Samples;             // Put here your samples
		[Header("Random Pitch")]
		public bool RandomPitch = true;         // if false random is deactivate
		public float Pitch_Min = 1;             // Randomize pitch when a sound is played. Minimum pitch
		public float Pitch_Max = 1.2f;              // Maximum pitch
		[Header("Random Volume")]
		public bool RandomVolume = true;            // if false random is deactivate
		public float Volume_Min = .8f;              // Randomize volume when a sound is played. Minimum volume
		public float Volume_Max = 1;                // Maximum volume
		private int tmp_Counter = 0;                    // Know which sample is currently playing


		void Start()
		{                                                           // -> Init
			s_Audio = GetComponent<AudioSource>();                                  // Access Audio Component
		}


		public void target_Play()
		{                                                   // -> Play sound on the same order as you put them inside variable Samples
			if (RandomPitch) s_Audio.pitch = Random.Range(Pitch_Min, Pitch_Max);        // Random pitch
			if (RandomVolume) s_Audio.volume = Random.Range(Volume_Min, Volume_Max);    // Radom volume

			if (Samples.Length > 0)
			{                                                   // Play sound
				s_Audio.PlayOneShot(Samples[tmp_Counter]);
			}
			tmp_Counter++;
			tmp_Counter = tmp_Counter % Samples.Length;
		}


	}
}