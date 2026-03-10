// Description	: Start playing a sound when button A is pressed. Stop the sound when the button is released

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TS.ZombieGames
{
	public class Target_Weapon_Type_3 : MonoBehaviour
	{


		public AudioClip[] s_audio;     // Sample to play
		private AudioSource s_Sound;        // AudioSource Component

		void Start()
		{
			s_Sound = GetComponent<AudioSource>();              // Access AudioSource component
		}


		void Update()
		{
			if (Input.GetKeyDown("a"))
			{                           // Start sound when button is pressed
				s_Sound.clip = s_audio[0];
				s_Sound.Play();
			}
			if (Input.GetKeyUp("a"))
			{                           // Stop sound when button is released
				s_Sound.Stop();
			}
		}
	}
}