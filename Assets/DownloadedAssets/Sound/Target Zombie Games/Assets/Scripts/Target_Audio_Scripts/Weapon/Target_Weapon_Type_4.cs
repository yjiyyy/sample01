// Description	: Use this scripts if your your weapon need to use a start sound a idle sound and a stop sound

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TS.ZombieGames
{
	public class Target_Weapon_Type_4 : MonoBehaviour
	{

		public string State = "Off";        // Use to know the state of this object
		public AudioClip[] s_audio;         // Sample to use
		private AudioSource s_Sound;            // AudioSource Component
		private bool one = false;                           // use to prevent bug

		void Start()
		{
			s_Sound = GetComponent<AudioSource>();                  // Access AudioSource Component
		}


		void Update()
		{
			if (Input.GetKeyDown("a") && State == "Off")
			{               // Play Start sample
				State = "On";
				s_Sound.loop = false;
				s_Sound.clip = s_audio[0];
				s_Sound.Play();
				one = true;
			}

			if (State == "On" && !s_Sound.isPlaying)
			{                   // Play Idle sample
				s_Sound.loop = true;
				s_Sound.clip = s_audio[1];
				s_Sound.Play();
				State = "Idle";
			}

			if (Input.GetKeyDown("a") && State == "Idle"
				|| !one && Input.GetKeyDown("a") && State == "On")
			{       // PLay Stop sample
				State = "Off";
				s_Sound.loop = false;
				s_Sound.clip = s_audio[2];
				s_Sound.Play();
			}

			one = false;
		}

	}
}