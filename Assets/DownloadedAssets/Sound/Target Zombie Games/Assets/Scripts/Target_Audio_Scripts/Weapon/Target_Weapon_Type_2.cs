// Description	: Play a sound when you press A
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TS.ZombieGames
{
	public class Target_Weapon_Type_2 : MonoBehaviour
	{

		public AudioClip[] s_audio; // Sample
		private AudioSource s_Sound;    // AudioSource component


		void Start()
		{
			s_Sound = GetComponent<AudioSource>();          // Access AudioSource component
		}

		void Update()
		{
			if (Input.GetKeyDown("a"))
				s_Sound.PlayOneShot(s_audio[0]);            // Play a sound when A is pressed
		}
	}
}