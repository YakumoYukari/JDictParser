using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using System.Windows.Forms;

namespace JDictParser
{
	class Program
	{
		[STAThreadAttribute]
		static void Main(string[] args)
		{
			JDictParser.Load("kanjidic2.xml", "JMdict_e.xml", "heisigkeywords.txt");

			/*
			Console.WriteLine("\n--------------------------------------------------\n");

			for (int i = 4; i > 0; i--)
			{
				JDictParser.PrintQueryResult(new JDictParser.SelectionFunctionKanji((k) =>
				{
					return k.JLPTLevel == i;
				}));

				Console.WriteLine("\n--------------------------------------------------\n");
			}

			Console.WriteLine("Searching for 立:\n" + JDictParser.LookupKanji('立'));
			*/

			/*
			JDictParser.CreateAnkiWordDeck("TestDeck.txt", JDictParser.SelectEntries(
				(e) =>
				{
					foreach (JDictParser.Kanji k in e.Kanji)
					{
						if (!k.Joyo) return false;
					}
					return e.NumberOfKanji == e.Word.Length && e.Word.Length <= 3;
				},
				(e) =>
				{
					int Sum = 0;
					foreach (JDictParser.Kanji k in e.Kanji)
					{
						Sum += k.StrokeCount;
					}
					return Sum;
				}
				));
			*/

			/*JDictParser.CreateAnkiKanjiDeck("TestKanjiDeck.txt", JDictParser.SelectKanji(
				(e) =>
				{
					return !String.IsNullOrEmpty(e.HeisigKeyword);
				},
				(e) =>
				{
					return e.HeisigIndex;
				}
				));
			*/

			/*
			List<JDictParser.JDictEntry> HeisigWords;

			HeisigWords = JDictParser.SelectEntries(
				(e) =>
				{
					if (e.NumberOfKanji == 1 && e.Kanji[0].HeisigIndex > 0 && e.Kanji[0].HeisigIndex > 2042)
					{
						foreach (string s in e.Kanji[0].KunYomi)
							if (e.Reading == s.Replace(".", ""))
								return true;
					}

					return false;
				},
				(e) =>
				{
					return e.Kanji[0].HeisigIndex;
				});

			string cb = "";
			foreach (JDictParser.JDictEntry e in HeisigWords)
			{
				Console.Write(e.Word + "\t");
			}

			JDictParser.CreateAnkiWordDeck("HeisigTwoVocabDeck.txt", HeisigWords);

			//Clipboard.SetText(cb);

			Console.WriteLine("\nCount: " + HeisigWords.Count);
			*/

			List<JDictParser.Kanji> JiruKun;

			char[] splitter = new char[] { '.' };

			JiruKun = JDictParser.SelectKanji(
				(e) =>
				{
					foreach (string s in e.KunYomi)
						if (s.Split(splitter)[0].Length >= 7)
							return true;

					return false;
				},
				(e) =>
				{
					int low = -1;
					foreach (string s in e.KunYomi)
					{
						int len = s.Split(splitter)[0].Length;
						if (len >= low)
						{
							low = len;
						}
					}
					return low;		
				});

			string res = "";

			foreach (JDictParser.Kanji k in JiruKun)
			{
				string lr = "";
				foreach (string s in k.KunYomi)
					if (s.Length > lr.Length)
						lr = s;
				res += (k.Character + ": " + lr) + "\n";
			}

			File.WriteAllText("results.txt", res);
			//Console.WriteLine("Count: {0}/{1}", JiruKun.Count, JDictParser.Entries.Count);

			Console.ReadKey();
		}
	}

	//Parses and generates data lists from EDICT for query or exporting to Anki decks
	public static class JDictParser
	{
		//Selection/sorting function delegates for external use
		public delegate bool SelectionFunction(JDictEntry a);
		public delegate bool SelectionFunctionKanji(Kanji a);
		public delegate int SortingFunction(JDictEntry a);
		public delegate int SortingFunctionKanji(Kanji a);

		//Database members
		public static List<Kanji> KanjiDB { get; private set; }
		public static List<JDictEntry> Entries { get; private set; }
		private static Kanji[] FastKanji;

		//XML parsing and database creation
		public static void Load(String KanjiFile, String DictFile, String HeisigFile)
		{
			ReadKanjiFile(KanjiFile);
			ReadHeisigFile(HeisigFile);
			ReadDictFile(DictFile);
		}
		private static void ReadKanjiFile(String file)
		{
			if (!File.Exists(file))
			{
				return;
			}

			XmlDocument doc = new XmlDocument();
			doc.Load(file);

			XmlNodeList nodes = doc.DocumentElement.SelectNodes("/kanjidic2/character");

			KanjiDB = new List<Kanji>();

			foreach (XmlNode node in nodes)
			{
				Kanji entry = new Kanji();

				entry.Character = node.SelectSingleNode("literal").InnerText.ElementAt(0);
				if (String.IsNullOrEmpty(RemoveKana(entry.Character + ""))) continue; //Something slipped through the cracks and isn't valid

				Int32.TryParse(node.SelectSingleNode("codepoint").SelectNodes("cp_value")[0].InnerText, System.Globalization.NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out entry.Unicode);

				if (node.SelectSingleNode("misc/grade") != null)
					Int32.TryParse(node.SelectSingleNode("misc").SelectSingleNode("grade").InnerText, out entry.Grade);
				else
					entry.Grade = 0;

				if (node.SelectSingleNode("misc/stroke_count") != null)
					Int32.TryParse(node.SelectSingleNode("misc/stroke_count").InnerText, out entry.StrokeCount);

				if (node.SelectSingleNode("misc/jlpt") != null)
					Int32.TryParse(node.SelectSingleNode("misc/jlpt").InnerText, out entry.JLPTLevel);

				foreach (XmlNode subnode in node.SelectNodes("dic_number/dic_ref[@dr_type='heisig']"))
					Int32.TryParse(subnode.InnerText, out entry.HeisigIndex);

				foreach (XmlNode subnode in node.SelectNodes("dic_number/dic_ref[@dr_type='heisig6']"))
					Int32.TryParse(subnode.InnerText, out entry.HeisigSixthIndex);

				if (node.SelectSingleNode("misc/freq") != null)
					Int32.TryParse(node.SelectSingleNode("misc/freq").InnerText, out entry.NewspaperFrequency);

				List<String> Meanings = new List<String>();
				List<String> KunYomis = new List<String>();
				List<String> OnYomis = new List<String>();
				List<String> NaNori = new List<String>();
				foreach (XmlNode subnode in node.SelectNodes("reading_meaning/rmgroup/meaning"))
				{
					if (subnode.Attributes.Count == 0)
						Meanings.Add(subnode.InnerText);
				}
				foreach (XmlNode subnode in node.SelectNodes("reading_meaning/rmgroup/reading[@r_type='ja_on']"))
				{
					OnYomis.Add(subnode.InnerText);
				}
				foreach (XmlNode subnode in node.SelectNodes("reading_meaning/rmgroup/reading[@r_type='ja_kun']"))
				{
					KunYomis.Add(subnode.InnerText);
				}
				foreach (XmlNode subnode in node.SelectNodes("reading_meaning/nanori"))
				{
					NaNori.Add(subnode.InnerText);
				}
				entry.Meanings = Meanings.ToArray();
				entry.KunYomi = KunYomis.ToArray();
				entry.OnYomi = OnYomis.ToArray();
				entry.NameReadings = NaNori.ToArray();

				//Some final calculations for values

				entry.LearnedInGradeSchool = entry.Grade <= 6 && entry.Grade > 0;
				entry.LearnedInJuniorHigh = entry.Grade == 8;
				entry.Jinmeiyo = entry.Grade >= 9;
				entry.Joyo = entry.Grade > 0 && !entry.Jinmeiyo;

				KanjiDB.Add(entry);

				if (KanjiDB.Count % 100 == 0 || KanjiDB.Count == nodes.Count)
				{
					Console.WriteLine("Kanji Progress {0:0.00}%: {1}/{2}", ((float)KanjiDB.Count) / nodes.Count * 100, KanjiDB.Count, nodes.Count);
				}
			}
			BuildFastKanjiLookup();
		}
		private static void ReadDictFile(String file)
		{
			if (!File.Exists(file))
			{
				return;
			}

			XmlDocument doc = new XmlDocument();
			doc.Load(file);

			XmlNodeList nodes = doc.DocumentElement.SelectNodes("/JMdict/entry");

			Entries = new List<JDictEntry>();

			foreach (XmlNode node in nodes)
			{
				XmlNodeList KanjiNodes = null;
				if (node.SelectSingleNode("k_ele") != null)
					KanjiNodes = node.SelectNodes("k_ele");

				if (KanjiNodes != null)
					foreach (XmlNode x in KanjiNodes)
					{
						XmlNode x2 = x.SelectSingleNode("keb");
						if (x2 != null)
							Entries.Add(CreateVocabEntry(node, x2.InnerText));
					}

				if (KanjiNodes == null || KanjiNodes.Count == 0)
					Entries.Add(CreateVocabEntry(node));

				if (Entries.Count % 1000 == 0 || Entries.Count == nodes.Count)
				{
					Console.WriteLine("Dict Progress {0:0.00}%: {1}/{2}", ((float)Entries.Count) / nodes.Count * 100, Entries.Count, nodes.Count);
				}
			}
		}
		private static void ReadHeisigFile(String file)
		{
			if (!File.Exists(file))
			{
				return;
			}

			string[] FileContents = File.ReadAllLines(file);
			
			Console.WriteLine("Reading Heisig Keywords...");
			int Current = 0;
			foreach(string s in FileContents)
			{
				if (String.IsNullOrEmpty(s)) { ++Current; continue; }
				string[] parts = s.Split(new char[] {':'});

				Kanji k = LookupKanji(parts[0].ToCharArray()[0]);
				if (k == null) { ++Current; continue; }

				k.HeisigKeyword = parts[1];

				if (++Current % 100 == 0 || Current == FileContents.Length)
				{
					Console.WriteLine("Heisig Progress {0:0.00}%: {1}/{2}", ((float)Current) / FileContents.Length * 100, Current, FileContents.Length);
				}
			}
		}

		//Debug functions
		public static List<JDictEntry> SelectEntriesWithSplitPOS()
		{
			List<JDictEntry> ret = new List<JDictEntry>();

			foreach (JDictEntry e in Entries)
			{
				bool flag = false;
				foreach (Sense s in e.Senses)
				{
					if (!flag && s.PartsOfSpeech.Length > 0)
						flag = true;
					else if (flag && s.PartsOfSpeech.Length > 0)
						ret.Add(e);
				}
			}

			return ret;
		}
		public static List<JDictEntry> SelectEntriesWithEmptyWords()
		{
			List<JDictEntry> ret = new List<JDictEntry>();

			foreach (JDictEntry e in Entries)
			{
				if (String.IsNullOrEmpty(e.Word))
					ret.Add(e);
			}

			return ret;
		}

		//Generic selection functions
		public static List<Kanji> SelectKanji(SelectionFunctionKanji s, SortingFunctionKanji sort = null)
		{
			var query = from e in KanjiDB
						where s(e)
						select e;

			if (sort != null)
				return query.OrderBy((e) => { return sort(e); }).ToList();

			return query.ToList();
		}
		public static List<JDictEntry> SelectEntries(SelectionFunction s, SortingFunction sort = null)
		{
			var query = from e in Entries
						where s(e)
						select e;

			if (sort != null)
				return query.OrderBy((e) => { return sort(e); }).ToList();

			return query.ToList();
		}
		public static Kanji SelectKanji(char kanji)
		{
			return LookupKanji(kanji);
		}

		//Specific selection functions
		public static List<JDictEntry> SelectEntriesContainingKanji(char Kanji)
		{
			List<JDictEntry> ret;

			var query = from entry in Entries
						from kanji in entry.Kanji
						where kanji.Character == Kanji
						select entry;

			ret = query.ToList();

			return ret;
		}
		public static List<JDictEntry> SelectEntriesContainingMeaning(String def)
		{
			List<JDictEntry> ret;

			var query = from entry in Entries
						from sense in entry.Senses
						from meaning in sense.Meanings
						where meaning.Contains(def)
						select entry;

			ret = query.ToList();

			return ret;
		}
		public static List<JDictEntry> SelectEntriesContainingWord(String word)
		{
			List<JDictEntry> ret;

			var query = from entry in Entries
						where entry.Word.Contains(word)
						select entry;

			ret = query.ToList();

			return ret;
		}
		public static List<JDictEntry> SelectEntriesContainingPartOfSpeech(String pos)
		{
			List<JDictEntry> ret;

			var query = from entry in Entries
						from sense in entry.Senses
						from part in sense.PartsOfSpeech
						where part.Contains(pos)
						select entry;

			ret = query.ToList();

			return ret;
		}
		public static List<JDictEntry> SelectEntriesContainingReading(String reading)
		{
			List<JDictEntry> ret;

			var query = from entry in Entries
						where entry.Reading.Contains(reading)
						select entry;

			ret = query.ToList();

			return ret;
		}

		//Anki-importable text file generators
		public static void CreateAnkiWordDeck(String Filename, List<JDictEntry> Cards, int MaxEntries = -1)
		{
			if (!Filename.EndsWith(".txt")) Filename += ".txt";
			List<String> lines = new List<String>();
			lines.Add("Word\tJDIC Sequence Number\tReading\tKanji\tParts of Speech\tMeanings\tRelated Words");

			int Entries = 0;
			foreach (JDictEntry e in Cards)
			{
				if (MaxEntries > -1 && ++Entries > MaxEntries) break;

				String kj = "";
				foreach (Kanji k in e.Kanji)
					kj += k.Character + " ";
				if (kj.Length > 0)
					kj = kj.Substring(0, kj.Length - 1);

				String partsofspeech = "";
				String meanings = "";
				foreach (Sense s in e.Senses)
				{
					foreach (String ps in s.PartsOfSpeech)
					{
						string ts = "";
						if (ps.Contains("("))
							ts = ps.Substring(0, ps.IndexOf("(")).Trim(); //Cut the (explanations) out of the parts of speech
						else
							ts = ps;

						if (!partsofspeech.Contains(ts + ","))
							partsofspeech += ts + ", ";
					}

					int Count = 0;
					foreach (String m in s.Meanings)
					{
						meanings += (++Count) + ": " + m + "<br>";
					}

					meanings += "<br>";
				}
				if (partsofspeech.Length > 0)
					partsofspeech = partsofspeech.Substring(0, partsofspeech.Length - 2);
				while (meanings.EndsWith("<br>"))
					meanings = meanings.Substring(0, meanings.Length - 4);

				String xref = "";
				foreach (String s in e.XRef)
				{
					xref += s + "; ";
				}
				if (xref.Length > 0)
					xref = xref.Substring(0, xref.Length - 2);

				String line = String.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", e.Word, e.SequenceNumber, e.Reading, kj, partsofspeech, meanings, xref);
				lines.Add(line);
			}

			File.WriteAllLines(Filename, lines);
		}
		public static void CreateAnkiKanjiDeck(String Filename, List<Kanji> Cards, int MaxEntries = -1)
		{
			if (!Filename.EndsWith(".txt")) Filename += ".txt";
			List<String> lines = new List<String>();
			lines.Add("Kanji\tOn'yomi\tKun'yomi\tJinmeiyo\tGrade\tStroke Count\tHeisig\tHeisig 6th\tNewspaper Frequency\tMeanings\tHeisig Keyword");

			int Entries = 0;
			foreach (Kanji kanji in Cards)
			{
				if (MaxEntries > -1 && ++Entries > MaxEntries) break;

				String OnY = "";
				foreach (String s in kanji.OnYomi)
					OnY += s + ", ";
				if (OnY.Length > 0)
					OnY = OnY.Substring(0, OnY.Length - 2);

				String KunY = "";
				foreach (String s in kanji.KunYomi)
					KunY += s + ", ";
				if (KunY.Length > 0)
					KunY = KunY.Substring(0, KunY.Length - 2);

				String J = "";
				foreach (String s in kanji.NameReadings)
					J += s + ", ";
				if (J.Length > 0)
					J = J.Substring(0, J.Length - 2);

				String m = "";
				foreach (String s in kanji.Meanings)
					m += s + ", ";
				if (m.Length > 0)
					m = m.Substring(0, m.Length - 2);

				String line = String.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}", kanji.Character, OnY, KunY, J, kanji.Grade, kanji.StrokeCount, kanji.HeisigIndex, kanji.HeisigSixthIndex, kanji.NewspaperFrequency, m, kanji.HeisigKeyword);
				lines.Add(line);
			}

			File.WriteAllLines(Filename, lines);
		}

		//Printing and file output functions
		public static void PrintQueryResult(SelectionFunctionKanji s, String file = "")
		{
			List<Kanji> lst = SelectKanji(s);
			var lstSorted = lst.OrderBy((k) => { return k.StrokeCount; });

			if (!String.IsNullOrEmpty(file))
			{
				var v = from l in lstSorted
						select l.Character.ToString();
				File.WriteAllLines(file, v);
				return;
			}

			int Count = 0;
			foreach (Kanji e in lstSorted)
			{
				Console.Write(e.Character + " ");
				if (++Count % 20 == 0)
					Console.WriteLine();
			}
			Console.WriteLine("\nMatching Entries: " + lst.Count);
		}
		public static void PrintQueryResult(SelectionFunction s, String file = "")
		{
			List<JDictEntry> lst = SelectEntries(s);
			var lstSorted = lst.OrderBy((k) => { return k.Word.Length; });

			if (!String.IsNullOrEmpty(file))
			{
				var v = from l in lstSorted
						select l.Word;
				File.WriteAllLines(file, v);
				return;
			}

			int Count = 0;
			foreach (JDictEntry e in lstSorted)
			{
				Console.Write(e.Word + ",");
				if (++Count % 10 == 0)
					Console.WriteLine();
			}
			Console.WriteLine("\nMatching Entries: " + lst.Count);
		}

		//Helper functions
		public static bool ArrayContains(string s, string[] search)
		{
			for (int i = 0; i < search.Length; i++)
				if (s == search[i])
					return true;
			return false;
		}
		public static Kanji LookupKanji(char kanji)
		{
			if (FastKanji != null)
			{
				int index = KanjiIndex(kanji);
				if (index > -1)
					return FastKanji[index];
			}

			foreach (Kanji k in KanjiDB)
				if (k.Character == kanji)
					return k;
			return null;
		}
		public static string RemoveKana(string s)
		{
			return new String(GetCharsInRange(s, 0x4E00, 0x9FBF).ToArray<char>());
		}
		private static bool InRange(int a, int min, int max)
		{
			return (a >= min && a <= max);
		}
		private static IEnumerable<char> GetCharsInRange(string text, int min, int max)
		{
			if (String.IsNullOrEmpty(text)) return new List<char>();
			return text.Where(e => e >= min && e <= max);
		}
		private static int KanjiIndex(char kj)
		{
			if (kj < 0x4E00 || kj > 0x9FBF) return -1;
			return ((char)kj) - 0x4E00;
		}
		private static void BuildFastKanjiLookup()
		{
			Console.WriteLine("Building fast lookup table...");
			FastKanji = new Kanji[0x9FBF - 0x4E00 + 1];

			int Count = 0;
			foreach (Kanji k in KanjiDB)
			{
				FastKanji[KanjiIndex(k.Character)] = k;
				if (++Count % 100 == 0 || Count == KanjiDB.Count)
					Console.WriteLine(String.Format("Kanji Lookup Progress {0:0.00}%: {1}/{2}", (float)Count / KanjiDB.Count * 100, Count, KanjiDB.Count));
			}
			Console.WriteLine("Done building fast kanji index!");
		}
		private static JDictEntry CreateVocabEntry(XmlNode node, string kanjioverride = "")
		{
			JDictEntry entry = new JDictEntry();

			int.TryParse(node.SelectSingleNode("ent_seq").InnerText, out entry.SequenceNumber);

			if (String.IsNullOrEmpty(kanjioverride) && node.SelectSingleNode("k_ele") != null)
				entry.Word = node.SelectSingleNode("k_ele/keb").InnerText;
			else
				entry.Word = kanjioverride;

			entry.Reading = node.SelectSingleNode("r_ele/reb").InnerText;
			if (String.IsNullOrEmpty(entry.Word))
				entry.Word = entry.Reading;

			List<String> attributes = new List<String>();
			List<Sense> senses = new List<Sense>();
			List<String> xr = new List<String>();
			foreach (XmlNode subnode in node.SelectNodes("sense"))
			{
				Sense s = new Sense();

				foreach (XmlNode subsubnode in subnode.SelectNodes("xref"))
				{
					xr.Add(subsubnode.InnerText);
				}

				List<String> pos = new List<String>();

				foreach (XmlNode subsubnode in subnode.SelectNodes("pos"))
				{
					pos.Add(subsubnode.InnerText);
				}
				s.PartsOfSpeech = pos.ToArray();

				List<String> meanings = new List<String>();

				foreach (XmlNode subsubnode in subnode.SelectNodes("gloss"))
				{
					string tm = subsubnode.InnerText;
					if (tm.EndsWith("(P)"))
					{
						entry.Priority = true;
						tm = tm.Substring(0, tm.Length - 3);
					}
					meanings.Add(tm);
				}
				s.Meanings = meanings.ToArray();


				foreach (XmlNode subsubnode in subnode.SelectNodes("misc"))
				{
					attributes.Add(subsubnode.InnerText);
				}

				senses.Add(s);
			}
			entry.Attributes = attributes.ToArray();
			entry.Senses = senses.ToArray();
			entry.XRef = xr.ToArray();

			String KanjiOnly = RemoveKana(entry.Word);

			HashSet<Kanji> kanji = new HashSet<Kanji>();
			if (!String.IsNullOrEmpty(KanjiOnly))
			{
				for (int i = 0; i < KanjiOnly.Length; i++)
				{
					Kanji k = LookupKanji(KanjiOnly.ElementAt(i));
					if (k != null)
					{
						entry.NumberOfKanji++;
						if (!kanji.Contains(k))
							kanji.Add(k);
					}
				}
			}
			entry.Kanji = kanji.ToArray();

			return entry;
		}

		//Kana conversion functions
		public static string KatakanaToHiragana(string kana)
		{
			if (kana == null) return null;

			kana = kana.Replace("ッ", "っ");
			kana = kana.Replace("チ", "ち");
			kana = kana.Replace("シ", "し");
			kana = kana.Replace("ツ", "つ");
			kana = kana.Replace("ヅ", "づ");
			kana = kana.Replace("ヂ", "ぢ");
			kana = kana.Replace("ヮ", "ゎ");
			kana = kana.Replace("ャ", "ゃ");
			kana = kana.Replace("ィ", "ぃ");
			kana = kana.Replace("ュ", "ゅ");
			kana = kana.Replace("ェ", "ぇ");
			kana = kana.Replace("ョ", "ょ");
			kana = kana.Replace("カ", "か");
			kana = kana.Replace("キ", "き");
			kana = kana.Replace("ク", "く");
			kana = kana.Replace("ケ", "け");
			kana = kana.Replace("コ", "こ");
			kana = kana.Replace("サ", "さ");
			kana = kana.Replace("ス", "す");
			kana = kana.Replace("セ", "せ");
			kana = kana.Replace("ソ", "そ");
			kana = kana.Replace("タ", "た");
			kana = kana.Replace("テ", "て");
			kana = kana.Replace("ト", "と");
			kana = kana.Replace("ナ", "な");
			kana = kana.Replace("ニ", "に");
			kana = kana.Replace("ヌ", "ぬ");
			kana = kana.Replace("ネ", "ね");
			kana = kana.Replace("ノ", "の");
			kana = kana.Replace("ハ", "は");
			kana = kana.Replace("ヒ", "ひ");
			kana = kana.Replace("フ", "ふ");
			kana = kana.Replace("ヘ", "へ");
			kana = kana.Replace("ホ", "ほ");
			kana = kana.Replace("マ", "ま");
			kana = kana.Replace("ミ", "み");
			kana = kana.Replace("ム", "む");
			kana = kana.Replace("メ", "め");
			kana = kana.Replace("モ", "も");
			kana = kana.Replace("ヤ", "や");
			kana = kana.Replace("ユ", "ゆ");
			kana = kana.Replace("ヨ", "よ");
			kana = kana.Replace("ラ", "ら");
			kana = kana.Replace("リ", "り");
			kana = kana.Replace("ル", "る");
			kana = kana.Replace("レ", "れ");
			kana = kana.Replace("ロ", "ろ");
			kana = kana.Replace("ワ", "わ");
			kana = kana.Replace("ヲ", "を");
			kana = kana.Replace("ガ", "が");
			kana = kana.Replace("ギ", "ぎ");
			kana = kana.Replace("グ", "ぐ");
			kana = kana.Replace("ゲ", "げ");
			kana = kana.Replace("ゴ", "ご");
			kana = kana.Replace("ジ", "じ");
			kana = kana.Replace("ダ", "だ");
			kana = kana.Replace("デ", "で");
			kana = kana.Replace("ド", "ど");
			kana = kana.Replace("バ", "ば");
			kana = kana.Replace("ビ", "び");
			kana = kana.Replace("ブ", "ぶ");
			kana = kana.Replace("ベ", "べ");
			kana = kana.Replace("ボ", "ぼ");
			kana = kana.Replace("パ", "ぱ");
			kana = kana.Replace("ピ", "ぴ");
			kana = kana.Replace("プ", "ぷ");
			kana = kana.Replace("ペ", "ぺ");
			kana = kana.Replace("ポ", "ぽ");
			kana = kana.Replace("ザ", "ざ");
			kana = kana.Replace("ズ", "ず");
			kana = kana.Replace("ゼ", "ぜ");
			kana = kana.Replace("ゾ", "ぞ");
			kana = kana.Replace("ァ", "ぁ");
			kana = kana.Replace("ィ", "ぃ");
			kana = kana.Replace("ゥ", "ぅ");
			kana = kana.Replace("ェ", "ぇ");
			kana = kana.Replace("ォ", "ぉ");
			kana = kana.Replace("ン", "ん");
			kana = kana.Replace("ア", "あ");
			kana = kana.Replace("イ", "い");
			kana = kana.Replace("ウ", "う");
			kana = kana.Replace("エ", "え");
			kana = kana.Replace("オ", "お");

			return kana;
		}

		//Used member classes
		public class JDictEntry
		{
			public int SequenceNumber;
			public String Word;
			public String Reading;
			public Sense[] Senses;
			public String[] XRef;
			public bool Priority;
			public Kanji[] Kanji; //Does not include duplicates
			public int NumberOfKanji; //Counts duplicates
			public String[] Attributes;
		}
		public class Sense
		{
			public String[] PartsOfSpeech;
			public String[] Meanings;
		}
		public class Kanji
		{
			public char Character;
			public String[] OnYomi;
			public String[] KunYomi;
			public String[] NameReadings;
			public String[] Meanings;
			public String HeisigKeyword;

			public int StrokeCount;
			public int Grade;
			public int NewspaperFrequency;

			public bool Joyo;
			public bool LearnedInGradeSchool;
			public bool LearnedInJuniorHigh;
			public bool Jinmeiyo;

			public int JLPTLevel;
			public int HeisigIndex;
			public int HeisigSixthIndex;
			public int Unicode;

			public override String ToString()
			{
				String ret = "";

				ret += String.Format("[{0}]({2}):[{1}]\r\n", Character, Unicode, StrokeCount);
				if (OnYomi.Length > 0)
					ret += String.Format("OnYomi: {0}\r\n", OnYomi);
				if (KunYomi.Length > 0)
					ret += String.Format("KunYomi: {0}\r\n", KunYomi);
				if (NameReadings.Length > 0)
					ret += String.Format("NameReadings: {0}\r\n", NameReadings);
				if (Meanings.Length > 0)
					ret += String.Format("Meanings: {0}\r\n", Meanings);
				ret += String.Format("Grade: {0}, JLPT: {1}\r\n", Grade, JLPTLevel);
				ret += String.Format("Heisig: {0}, Frequency: {1}\r\n", HeisigIndex, NewspaperFrequency);
				ret += String.Format("Heisig Keyword: {0}", HeisigKeyword);

				return ret;
			}
		}
	}
}
