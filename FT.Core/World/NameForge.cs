using System;
using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 국가 하나의 이름 재료. 실제 리그·구단·선수 이름은 쓸 수 없으므로 전부 가상이다.
    /// 국가마다 어감을 다르게 두어 이적 목록에서 출신을 구분할 수 있게 한다.
    /// </summary>
    public sealed class NationNames
    {
        public string Code;              // "NSL"
        public Nationality Nationality;  // 모델 enum 과의 연결
        public string Country;           // "노르셀"
        public string LeagueName;        // "노르셀 프리미어"
        public string LeagueShort;       // "NPL"
        public string SecondLeagueName;  // "노르셀 1부리그" — 2부 리그
        public string SecondLeagueShort;
        public string ThirdLeagueName;   // 3부 리그
        public string ThirdLeagueShort;
        public string Currency = "€";

        public string[] Cities;
        public string[] ClubPrefixes;    // 도시 앞에 붙는 것
        public string[] ClubSuffixes;    // 도시 뒤에 붙는 것
        public string[] StadiumWords;
        public string[] FirstNames;
        public string[] LastNames;
    }

    public static class NameForge
    {
        public static readonly NationNames Norsel = new()
        {
            Code = "NSL", Nationality = Nationality.NOR,
            Country = "노르셀", LeagueName = "노르셀 프리미어", LeagueShort = "NPL",
            SecondLeagueName = "노르셀 1부리그", SecondLeagueShort = "N1L",
            ThirdLeagueName = "노르셀 2부리그", ThirdLeagueShort = "N2L",
            Cities = new[]
            {
                "Vesthavn","Solberg","Kaldfjord","Austmark","Rygge","Hjelm","Nordvik","Storhaug",
                "Lindal","Brattvik","Enger","Flatvold","Grimstad","Halden","Isfjell","Jorvik",
                "Kvalnes","Lysaker","Myrland","Osterbo",
                "Rindal","Sorvik","Trollhaug","Ulvik","Vangsnes","Aurland","Bodvik","Dovre",
                "Eikland","Fosnes","Gaular","Hovik","Ingdal","Jostedal","Kirkenes","Leknes",
                "Mosjoen","Nesbyen","Odda","Porsanger",
                // 3부
                "Aarnes","Bergheim","Drangedal","Eidsvag","Finnoy","Gjerstad","Hamarvik","Innset",
                "Jondal","Kolsas","Langnes","Moldefjord","Narvang","Oksfjord","Prestvik","Randal",
                "Skjervoy","Tjeldsund","Vikersund","Ytteroy"
            },
            ClubPrefixes = new[] { "IF", "BK", "FK", "IK" },
            ClubSuffixes = new[] { "", "" },
            StadiumWords = new[] { "Arena", "Stadion", "Park" },
            FirstNames = new[]
            {
                "Jonas","Mikal","Aren","Sven","Erik","Rune","Olav","Nils","Kasper","Teodor",
                "Halvard","Birger","Steinar","Vetle","Arne","Torvald","Leif","Gunnar","Eivind","Sigurd",
                "Trygve","Audun","Vidar","Knut","Sindre","Harald","Bjarne","Oddvar","Tormod","Helge",
                "Asbjorn","Frode","Ivar","Magnus","Stian","Even","Jarle","Terje","Ulf","Viggo"
            },
            LastNames = new[]
            {
                "Vestre","Halden","Brenn","Kvist","Agren","Falk","Norrby","Solvik","Draven","Osk",
                "Ryland","Haugset","Lindqvist","Bjornstad","Markussen","Hovden","Strandli","Aalberg","Tveit","Rognstad",
                "Bergheim","Dahlstrom","Engevik","Fjelstad","Gulbrandsen","Hestnes","Ingdal","Jostad","Kvalheim","Lomsdal",
                "Melby","Naesheim","Oyvind","Prestrud","Rodland","Skarsgard","Torsvik","Ulvestad","Viken","Wergeland"
            }
        };

        public static readonly NationNames Velmara = new()
        {
            Code = "VLM", Nationality = Nationality.VEL,
            Country = "벨마라", LeagueName = "리가 벨마라", LeagueShort = "LVM",
            SecondLeagueName = "세군다 벨마라", SecondLeagueShort = "SVM",
            ThirdLeagueName = "테르세라 벨마라", ThirdLeagueShort = "TVM",
            Cities = new[]
            {
                "Castelvera","Montara","Ribalta","Sorrevia","Almadén","Puerto Nío","Valbranca","Serrano",
                "Olmeda","Caldera","Treviso Mar","Linares","Bellaroca","Quintanar","Marbela","Esparra",
                "Navalta","Corvera","Jerezal","Altamora",
                "Baldaroca","Calpena","Durango Vell","Encinar","Fontanar","Guadalema","Herreros","Isbana",
                "Lavandeira","Mirasol","Nerja Alta","Ourense Vell","Peñalba","Roquetas","Sanlucar","Tarraco",
                "Ubrique","Valdepeña","Xalada","Zurbarán",
                // 3부
                "Andújar Viejo","Berlanga","Cardeñosa","Dosaguas","Espinal","Formentor","Grazalema","Hinojosa",
                "Illescas","Jumilla","Lebrija","Monforte","Niebla","Oliveros","Palomares","Requena",
                "Sotillo","Trujillano","Verdejo","Yecla Alta"
            },
            ClubPrefixes = new[] { "CF", "AC", "Real", "Atlético" },
            ClubSuffixes = new[] { "", "CF", "FC" },
            StadiumWords = new[] { "Estadio", "Campo", "Coliseo" },
            FirstNames = new[]
            {
                "Mateo","Nicolás","Álvaro","Rubén","Iker","Dario","Salvio","Emilio","Joaquín","Tomás",
                "Bruno","Lucio","Rafa","Adrián","Hugo","Ismael","Víctor","Pablo","Gael","Cristo",
                "Aitor","Borja","Cesc","Diego","Eloy","Fran","Gonzalo","Héctor","Isco","Javi",
                "Koke","Leandro","Marco","Nando","Óscar","Pau","Quique","Raúl","Sergi","Unai"
            },
            LastNames = new[]
            {
                "Merlo","Calder","Vilanova","Reyes-Mota","Sabino","Arranz","Olmedo","Berruti","Quintela","Navas",
                "Cortines","Duarte","Esparza","Falcó","Guzmán","Herrera","Illescas","Jiménez","Lorca","Maldonado",
                "Nogueira","Ordóñez","Peralta","Quesada","Ramírez-Soto","Salgado","Tejedor","Urbina","Vallejo","Zamorano",
                "Arjona","Benavides","Carvajal","Delgado","Echevarría","Fuentes","Gallardo","Hinojosa","Iriarte","Juárez"
            }
        };

        public static readonly NationNames Hallbrun = new()
        {
            Code = "HBR", Nationality = Nationality.KAR,
            Country = "할브룬", LeagueName = "할브룬 분데스리가", LeagueShort = "HBL",
            SecondLeagueName = "할브룬 2.분데스리가", SecondLeagueShort = "H2L",
            ThirdLeagueName = "할브룬 3.리가", ThirdLeagueShort = "H3L",
            Cities = new[]
            {
                "Hallbrunn","Eisenthal","Rotenberg","Kalkstein","Weidach","Lindenau","Scharfeck","Ostheim",
                "Grünwald","Bachhofen","Treuberg","Mühlstadt","Nordhain","Felsenau","Amselbach","Dornfeld",
                "Steinau","Wolfsheim","Zellingen","Quernbach",
                "Adlerstein","Birkenfeld","Clausthal","Dankerode","Erlenbach","Fichtenau","Goldbach","Hainburg",
                "Illertal","Jagsthausen","Kronberg","Lauenstein","Marbach","Neustetten","Oberried","Pfalzgrafen",
                "Rheinau","Schwarzach","Tannberg","Wiesenthal",
                // 3부
                "Altenburg","Bruckmühl","Eichfeld","Freiberg","Grabenau","Hohenstein","Immenried","Kirchdorf",
                "Lohbach","Merzenau","Nussdorf","Ortenberg","Pleinfeld","Riedlingen","Sulzbach","Thalheim",
                "Vogtland","Waldkirch","Zehnhausen","Bergfelde"
            },
            ClubPrefixes = new[] { "SV", "FC", "TSV", "SC" },
            ClubSuffixes = new[] { "", "04", "08", "1911" },
            StadiumWords = new[] { "Arena", "Stadion", "Sportpark" },
            FirstNames = new[]
            {
                "Lennart","Matthias","Jonas","Felix","Tobias","Kilian","Moritz","Ansgar","Reinhold","Veit",
                "Niklas","Sebastian","Florian","Hendrik","Korbinian","Dominik","Emil","Bastian","Raphael","Julian",
                "Leon","Marius","Thilo","Ulrich","Valentin","Benedikt","Clemens","Detlef","Gregor","Hannes",
                "Jorg","Konstantin","Lukas","Markus","Nico","Oskar","Philipp","Quirin","Roland","Simon"
            },
            LastNames = new[]
            {
                "Brennecke","Hofmeister","Kaltenbach","Lindner","Mertens","Nussbaum","Ostermann","Pfeiffer","Rauschen","Schattner",
                "Treichel","Vogler","Wendland","Ziegenhain","Eberhart","Dreyer","Grünbaum","Haberland","Jarosch","Krumm",
                "Lauterbach","Mauerhofer","Neuberger","Oswald","Pretzel","Reinhardt","Stahlberg","Tannhauser","Uhlmann","Walther",
                "Achterberg","Bruckmann","Dittmar","Engelhardt","Fahrner","Gerstner","Himmelreich","Jungwirth","Kellermann","Lohmann"
            }
        };

        public static readonly NationNames Sorrane = new()
        {
            Code = "SRN", Nationality = Nationality.SOL,
            Country = "소란", LeagueName = "소란 세리에", LeagueShort = "SRA",
            SecondLeagueName = "소란 세리에 B", SecondLeagueShort = "SRB",
            ThirdLeagueName = "소란 세리에 C", ThirdLeagueShort = "SRC",
            Cities = new[]
            {
                "Soranetta","Vercalli","Montelupo","Bastiana","Fiorano","Pradelle","Castagno","Lucerno",
                "Amandola","Riviera Dusa","Tornello","Grimaldi","Sarzano","Bellamonte","Oleggio","Petrara",
                "Novacasa","Carrucci","Veneto Alto","Zanetta",
                "Arezzano","Bellagio Basso","Castelrotto","Dolcedo","Empolino","Faenzaro","Grosseto Nuovo","Imperia Alta",
                "Lanciano","Macerata Vecchia","Nocera Sud","Orvietano","Pescarello","Rimini Alto","Sondrio","Terracina",
                "Urbino Basso","Vercelli Nuovo","Zagarolo","Belluno Alto",
                // 3부
                "Anzano","Brindisano","Cortemaggio","Druento","Esino Basso","Frosinara","Gubbiano","Ischiano",
                "Lentate","Morlupo","Nepezzano","Olginate","Pontremolo","Rovigano","Sestriere","Tolentino Vecchio",
                "Ugento","Valmontone","Zollino","Cassano Alto"
            },
            ClubPrefixes = new[] { "AS", "US", "Inter", "AC" },
            ClubSuffixes = new[] { "", "Calcio", "1909" },
            StadiumWords = new[] { "Stadio", "Arena", "Campo" },
            FirstNames = new[]
            {
                "Lorenzo","Matteo","Riccardo","Gianluca","Alessio","Davide","Federico","Stefano","Pietro","Andrea",
                "Simone","Tommaso","Emanuele","Fabrizio","Marcello","Nicolò","Enrico","Vito","Cristiano","Dario",
                "Alberto","Bruno","Carmine","Daniele","Elia","Filippo","Giacomo","Leonardo","Massimo","Nicola",
                "Orlando","Paolo","Raffaele","Salvatore","Tiziano","Umberto","Valerio","Luca","Mirko","Sandro"
            },
            LastNames = new[]
            {
                "Bellandi","Carosi","Donatelli","Esposito-Nava","Fabbrini","Grisanti","Loconte","Marzullo","Novelli","Oriani",
                "Pedrazzi","Rovere","Santarelli","Tagliabue","Ubaldi","Vannucci","Zarella","Beltrame","Canducci","Ferrero",
                "Grimaldi","Iannone","Lombardi","Mazzanti","Nocera","Ottaviani","Paganini","Quarta","Ricciardi","Scalise",
                "Tortorella","Ventura","Zambrano","Altieri","Bonaventura","Cestari","Delvecchio","Fioravanti","Gagliardi","Innocenti"
            }
        };

        public static readonly IReadOnlyList<NationNames> All =
            new[] { Norsel, Velmara, Hallbrun, Sorrane };

        public static NationNames ByCode(string code)
        {
            foreach (var n in All) if (n.Code == code) return n;
            return Norsel;
        }

        /// <summary>도시 이름으로 구단 이름을 만든다. index 로 접사를 돌려 같은 리그 안에서 겹치지 않게 한다.</summary>
        public static (string name, string shortName) ClubName(NationNames n, string city, int index)
        {
            string prefix = n.ClubPrefixes[index % n.ClubPrefixes.Length];
            string suffix = n.ClubSuffixes[(index / n.ClubPrefixes.Length) % n.ClubSuffixes.Length];

            // 접두와 접미가 겹치면("CF Treviso Mar CF") 접미를 버린다.
            if (suffix == prefix) suffix = "";

            string full = string.IsNullOrEmpty(suffix)
                ? $"{prefix} {city}"
                : $"{prefix} {city} {suffix}";

            // 약칭은 도시명 앞 3글자 — 순위표 폭을 일정하게 유지한다.
            string core = city.Replace(" ", "");
            string sn = core.Length >= 3 ? core.Substring(0, 3).ToUpperInvariant() : core.ToUpperInvariant();
            return (full, sn);
        }

        public static string StadiumName(NationNames n, Random rng, string city)
            => $"{city} {n.StadiumWords[rng.Next(n.StadiumWords.Length)]}";
    }
}
