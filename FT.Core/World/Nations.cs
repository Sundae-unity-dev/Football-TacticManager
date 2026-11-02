using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 실제 나라 목록. <b>나라 이름과 도시 이름만 실제</b>이고, 리그·구단·선수는 전부 지어낸다.
    ///
    /// 나라 이름("스페인")은 누가 독점하는 상표가 아니고 도시 이름도 그렇다. 반면
    /// 리그 이름("라리가"), 구단 이름과 엠블럼, 실존 선수 이름, 대회 이름("챔피언스리그"),
    /// 협회 로고는 전부 주인이 있다. 그래서 리그는 "스페인 1부"처럼 부르고, 구단은
    /// 도시 + 흔한 접두사로 만들고, 선수는 언어권 이름 풀에서 뽑는다.
    ///
    /// 실제 리그의 팀 수·연고지 배치를 그대로 베끼지도 않는다. 누가 봐도 그 리그면
    /// 이름을 쓰지 않았어도 빌미가 된다.
    ///
    /// <b>부 수가 곧 비용이다.</b> 3부까지 두는 나라는 구단 60개·선수 1,500명,
    /// 1부만 두는 나라는 20개·500명이다. 축구가 깊은 나라만 3부까지 둔다.
    /// </summary>
    public static class Nations
    {
        static NationNames N(string code, Nationality nat, string country, int tiers, float strength,
                             string region,
                             string[] cities, string[] prefixes, string[] suffixes,
                             string[] stadium, string[] first, string[] last)
            => new NationNames
            {
                Code = code, Nationality = nat, Country = country,
                Tiers = tiers, Strength = strength, Region = region,
                LeagueName = $"{country} 1부", LeagueShort = code + "1",
                SecondLeagueName = $"{country} 2부", SecondLeagueShort = code + "2",
                ThirdLeagueName = $"{country} 3부", ThirdLeagueShort = code + "3",
                Cities = cities, ClubPrefixes = prefixes, ClubSuffixes = suffixes,
                StadiumWords = stadium, FirstNames = first, LastNames = last,
            };

        // 구단 이름의 결. 나라마다 붙는 말이 다르다.
        static readonly string[] EngPre = { "", "", "", "AFC" };
        static readonly string[] EngSuf = { "City", "United", "Town", "Rovers", "Albion", "County" };
        static readonly string[] EspPre = { "CF", "CD", "UD", "Atletico", "Racing" };
        static readonly string[] ItaPre = { "AC", "AS", "US", "FC", "SS" };
        static readonly string[] GerPre = { "FC", "SV", "VfL", "TSV", "SC" };
        static readonly string[] FraPre = { "FC", "AS", "US", "SC", "RC" };
        static readonly string[] NedPre = { "FC", "SC", "VV", "AZ" };
        static readonly string[] PorPre = { "SC", "CD", "FC", "AD" };
        static readonly string[] BraPre = { "EC", "SC", "AA", "CR" };
        static readonly string[] ArgPre = { "CA", "CD", "AC" };
        static readonly string[] NorPre = { "IF", "BK", "FK", "IK" };
        static readonly string[] SlvPre = { "FK", "SK", "Dynamo", "Lokomotiv" };
        static readonly string[] AsiaSuf = { "FC", "United", "SC" };
        static readonly string[] None = { "" };
        static readonly string[] NonePre = { "" };

        static readonly string[] StEng = { "Stadium", "Park", "Ground" };
        static readonly string[] StEsp = { "Estadio", "Campo" };
        static readonly string[] StIta = { "Stadio", "Arena" };
        static readonly string[] StGer = { "Stadion", "Arena", "Park" };
        static readonly string[] StFra = { "Stade", "Parc" };
        static readonly string[] StGen = { "Stadium", "Arena", "Park" };

        /// <summary>
        /// 새 게임의 기본 — <b>3부까지 갖춘 네 나라</b>. 구단 240개·선수 6,000명으로
        /// 예전 세계와 같은 크기다.
        ///
        /// 전부(49개국)를 넣으면 구단 1,520개·선수 38,000명이 되고 세이브가
        /// 0.6MB 에서 3.8MB 로, 저장이 0.1초에서 1.4초(브라우저는 그 두 배)로 늘어난다.
        /// 그래서 <b>고르는 것</b>으로 둔다 — 많이 넣을수록 무거워진다.
        /// </summary>
        public static IReadOnlyList<NationNames> Default =>
            new[] { ByCode("ENG"), ByCode("ESP"), ByCode("ITA"), ByCode("GER") };

        public static NationNames ByCode(string code)
        {
            foreach (var n in All) if (n.Code == code) return n;
            return All[0];
        }

        public static readonly IReadOnlyList<NationNames> All = new[]
        {
            // ── 유럽 ──────────────────────────────────────────────────
            N("ENG", Nationality.ENG, "잉글랜드", 3, 1.00f, "유럽", new[]
            {
                "Manchester","Liverpool","Leeds","Sheffield","Newcastle","Nottingham","Leicester","Southampton",
                "Brighton","Norwich","Ipswich","Coventry","Derby","Stoke","Sunderland","Middlesbrough",
                "Blackburn","Bolton","Preston","Burnley","Portsmouth","Plymouth","Reading","Watford",
                "Luton","Hull","Swindon","Oxford","Cambridge","Exeter",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            N("ESP", Nationality.ESP, "스페인", 3, 1.00f, "유럽", new[]
            {
                "Valencia","Sevilla","Bilbao","Zaragoza","Malaga","Vigo","Gijon","Valladolid",
                "Pamplona","Granada","Murcia","Alicante","Cadiz","Cordoba","Santander","Oviedo",
                "Salamanca","Tarragona","Almeria","Huelva","Burgos","Leon","Albacete","Castellon",
                "Logrono","Toledo","Badajoz","Lugo",
            }, EspPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("ITA", Nationality.ITA, "이탈리아", 3, 0.97f, "유럽", new[]
            {
                "Torino","Genova","Bologna","Firenze","Verona","Parma","Udine","Bergamo",
                "Cagliari","Palermo","Catania","Bari","Lecce","Pescara","Ancona","Perugia",
                "Livorno","Modena","Brescia","Vicenza","Padova","Ferrara","Reggio","Salerno",
                "Trieste","Novara","Como","Pisa",
            }, ItaPre, None, StIta, NamePools.ItalianFirst, NamePools.ItalianLast),

            N("GER", Nationality.GER, "독일", 3, 0.97f, "유럽", new[]
            {
                "Hamburg","Koln","Frankfurt","Stuttgart","Dortmund","Bremen","Hannover","Nurnberg",
                "Leipzig","Dresden","Duisburg","Bochum","Mainz","Freiburg","Augsburg","Bielefeld",
                "Karlsruhe","Kiel","Rostock","Magdeburg","Aachen","Darmstadt","Paderborn","Heidenheim",
                "Regensburg","Osnabruck","Essen","Wiesbaden",
            }, GerPre, None, StGer, NamePools.GermanFirst, NamePools.GermanLast),

            N("FRA", Nationality.FRA, "프랑스", 3, 0.92f, "유럽", new[]
            {
                "Marseille","Lyon","Lille","Bordeaux","Nantes","Toulouse","Rennes","Strasbourg",
                "Montpellier","Nice","Saint-Etienne","Lens","Reims","Brest","Angers","Metz",
                "Caen","Nancy","Dijon","Troyes","Amiens","Le Havre","Clermont","Auxerre",
                "Grenoble","Bastia","Valenciennes","Sochaux",
            }, FraPre, None, StFra, NamePools.FrenchFirst, NamePools.FrenchLast),

            N("NED", Nationality.NED, "네덜란드", 3, 0.84f, "유럽", new[]
            {
                "Rotterdam","Eindhoven","Utrecht","Alkmaar","Groningen","Arnhem","Tilburg","Nijmegen",
                "Breda","Enschede","Heerenveen","Zwolle","Deventer","Dordrecht","Venlo","Sittard",
                "Waalwijk","Almelo","Emmen","Leeuwarden","Helmond","Maastricht","Haarlem","Leiden",
            }, NedPre, None, StGen, NamePools.DutchFirst, NamePools.DutchLast),

            N("POR", Nationality.POR, "포르투갈", 2, 0.84f, "유럽", new[]
            {
                "Porto","Braga","Guimaraes","Coimbra","Setubal","Faro","Aveiro","Leiria",
                "Viseu","Funchal","Portimao","Chaves","Famalicao","Moreira","Estoril","Barcelos",
                "Covilha","Tondela","Penafiel","Mafra",
            }, PorPre, None, StGen, NamePools.PortugueseFirst, NamePools.PortugueseLast),

            N("BEL", Nationality.BEL, "벨기에", 2, 0.78f, "유럽", new[]
            {
                "Brugge","Antwerpen","Gent","Luik","Charleroi","Genk","Mechelen","Leuven",
                "Kortrijk","Oostende","Hasselt","Namen","Eupen","Waregem","Dender","Lokeren",
                "Beveren","Westerlo","Seraing","Virton",
            }, NedPre, None, StGen, NamePools.DutchFirst, NamePools.DutchLast),

            N("TUR", Nationality.TUR, "튀르키예", 2, 0.78f, "유럽", new[]
            {
                "Istanbul","Ankara","Izmir","Bursa","Antalya","Adana","Konya","Trabzon",
                "Kayseri","Gaziantep","Samsun","Eskisehir","Denizli","Malatya","Sivas","Rize",
                "Erzurum","Mersin","Diyarbakir","Balikesir",
            }, NonePre, AsiaSuf, StGen, NamePools.TurkishFirst, NamePools.TurkishLast),

            N("SCO", Nationality.SCO, "스코틀랜드", 2, 0.70f, "유럽", new[]
            {
                "Glasgow","Edinburgh","Aberdeen","Dundee","Perth","Inverness","Motherwell","Kilmarnock",
                "Paisley","Falkirk","Ayr","Dunfermline","Livingston","Hamilton","Greenock","Stirling",
                "Elgin","Montrose","Arbroath","Alloa",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            N("RUS", Nationality.RUS, "러시아", 2, 0.76f, "유럽", new[]
            {
                "Moskva","Sankt-Peterburg","Kazan","Samara","Rostov","Krasnodar","Yekaterinburg","Novosibirsk",
                "Volgograd","Voronezh","Perm","Ufa","Sochi","Tula","Orenburg","Khimki",
                "Nizhny","Omsk","Chelyabinsk","Yaroslavl",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("UKR", Nationality.UKR, "우크라이나", 2, 0.70f, "유럽", new[]
            {
                "Kyiv","Lviv","Odesa","Kharkiv","Dnipro","Donetsk","Zaporizhzhia","Poltava",
                "Vinnytsia","Mariupol","Luhansk","Chernivtsi","Rivne","Ternopil","Kovalivka","Uzhhorod",
                "Kryvyi Rih","Mykolaiv","Sumy","Cherkasy",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("AUT", Nationality.AUT, "오스트리아", 1, 0.70f, "유럽", new[]
            {
                "Wien","Salzburg","Graz","Linz","Innsbruck","Klagenfurt","Wolfsberg","Ried",
                "Altach","Hartberg","St. Polten","Mattersburg","Wiener Neustadt","Villach","Bregenz","Dornbirn",
            }, GerPre, None, StGer, NamePools.GermanFirst, NamePools.GermanLast),

            N("SUI", Nationality.SUI, "스위스", 1, 0.70f, "유럽", new[]
            {
                "Zurich","Basel","Bern","Geneve","Lausanne","Luzern","St. Gallen","Lugano",
                "Sion","Winterthur","Thun","Aarau","Biel","Chiasso","Vaduz","Schaffhausen",
            }, GerPre, None, StGer, NamePools.GermanFirst, NamePools.GermanLast),

            N("DEN", Nationality.DEN, "덴마크", 1, 0.70f, "유럽", new[]
            {
                "Kobenhavn","Aarhus","Odense","Aalborg","Esbjerg","Randers","Horsens","Vejle",
                "Silkeborg","Brondby","Lyngby","Viborg","Sonderborg","Herning","Naestved","Hvidovre",
            }, NorPre, None, StGen, NamePools.NordicFirst, NamePools.NordicLast),

            N("SWE", Nationality.SWE, "스웨덴", 1, 0.64f, "유럽", new[]
            {
                "Stockholm","Goteborg","Malmo","Uppsala","Norrkoping","Helsingborg","Orebro","Vasteras",
                "Sundsvall","Kalmar","Halmstad","Jonkoping","Umea","Gavle","Linkoping","Boras",
            }, NorPre, None, StGen, NamePools.NordicFirst, NamePools.NordicLast),

            N("NOR", Nationality.NOR, "노르웨이", 1, 0.64f, "유럽", new[]
            {
                "Oslo","Bergen","Trondheim","Stavanger","Kristiansand","Tromso","Drammen","Bodo",
                "Sarpsborg","Haugesund","Lillestrom","Molde","Sandefjord","Alesund","Hamar","Fredrikstad",
            }, NorPre, None, StGen, NamePools.NordicFirst, NamePools.NordicLast),

            N("POL", Nationality.POL, "폴란드", 1, 0.66f, "유럽", new[]
            {
                "Warszawa","Krakow","Lodz","Wroclaw","Poznan","Gdansk","Szczecin","Lublin",
                "Katowice","Bialystok","Czestochowa","Gdynia","Plock","Legnica","Zabrze","Lubin",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("GRE", Nationality.GRE, "그리스", 1, 0.70f, "유럽", new[]
            {
                "Athina","Thessaloniki","Patra","Larissa","Volos","Iraklio","Ioannina","Serres",
                "Kavala","Xanthi","Rodos","Chania","Trikala","Lamia","Kalamata","Agrinio",
            }, NonePre, AsiaSuf, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("CZE", Nationality.CZE, "체코", 1, 0.70f, "유럽", new[]
            {
                "Praha","Brno","Ostrava","Plzen","Olomouc","Liberec","Hradec","Pardubice",
                "Zlin","Teplice","Jablonec","Mlada Boleslav","Karvina","Budejovice","Opava","Slovacko",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("SRB", Nationality.SRB, "세르비아", 1, 0.64f, "유럽", new[]
            {
                "Beograd","Novi Sad","Nis","Kragujevac","Subotica","Cacak","Krusevac","Zrenjanin",
                "Smederevo","Pancevo","Leskovac","Vranje","Sombor","Valjevo","Uzice","Sabac",
            }, SlvPre, None, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("CRO", Nationality.CRO, "크로아티아", 1, 0.66f, "유럽", new[]
            {
                "Zagreb","Split","Rijeka","Osijek","Zadar","Pula","Varazdin","Sibenik",
                "Dubrovnik","Karlovac","Vinkovci","Koprivnica","Slavonski Brod","Sisak","Bjelovar","Samobor",
            }, SlvPre, None, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            // ── 남아메리카 ────────────────────────────────────────────
            N("BRA", Nationality.BRA, "브라질", 3, 0.86f, "남아메리카", new[]
            {
                "Sao Paulo","Rio de Janeiro","Belo Horizonte","Porto Alegre","Salvador","Recife","Curitiba","Fortaleza",
                "Goiania","Belem","Manaus","Florianopolis","Vitoria","Natal","Campinas","Santos",
                "Cuiaba","Maceio","Joao Pessoa","Teresina","Londrina","Juiz de Fora","Sorocaba","Ribeirao Preto",
                "Uberlandia","Caxias","Bauru","Santa Maria",
            }, BraPre, None, StGen, NamePools.PortugueseFirst, NamePools.PortugueseLast),

            N("ARG", Nationality.ARG, "아르헨티나", 3, 0.82f, "남아메리카", new[]
            {
                "Buenos Aires","Rosario","Cordoba","La Plata","Mendoza","Santa Fe","Tucuman","Mar del Plata",
                "Salta","Bahia Blanca","San Juan","Neuquen","Resistencia","Posadas","Parana","Corrientes",
                "Santiago del Estero","San Luis","Formosa","Rio Cuarto","Avellaneda","Quilmes","Lanus","Banfield",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("URU", Nationality.URU, "우루과이", 1, 0.62f, "남아메리카", new[]
            {
                "Montevideo","Salto","Paysandu","Maldonado","Rivera","Tacuarembo","Melo","Durazno",
                "Colonia","Florida","Minas","San Jose","Artigas","Treinta y Tres","Rocha","Canelones",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("COL", Nationality.COL, "콜롬비아", 2, 0.68f, "남아메리카", new[]
            {
                "Bogota","Medellin","Cali","Barranquilla","Cartagena","Bucaramanga","Pereira","Manizales",
                "Ibague","Pasto","Monteria","Villavicencio","Neiva","Armenia","Cucuta","Tunja",
                "Popayan","Santa Marta","Valledupar","Palmira",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("CHI", Nationality.CHI, "칠레", 1, 0.62f, "남아메리카", new[]
            {
                "Santiago","Valparaiso","Concepcion","Antofagasta","Temuco","Iquique","La Serena","Rancagua",
                "Talca","Puerto Montt","Calama","Arica","Chillan","Osorno","Valdivia","Copiapo",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("PER", Nationality.PER, "페루", 1, 0.58f, "남아메리카", new[]
            {
                "Lima","Arequipa","Trujillo","Cusco","Chiclayo","Piura","Huancayo","Iquitos",
                "Tacna","Juliaca","Cajamarca","Ayacucho","Chimbote","Puno","Tarapoto","Huanuco",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            // ── 북중미 ────────────────────────────────────────────────
            N("MEX", Nationality.MEX, "멕시코", 2, 0.74f, "북중미", new[]
            {
                "Ciudad de Mexico","Guadalajara","Monterrey","Puebla","Toluca","Leon","Queretaro","Pachuca",
                "Tijuana","Torreon","Veracruz","Merida","San Luis Potosi","Morelia","Aguascalientes","Culiacan",
                "Juarez","Mazatlan","Hermosillo","Cancun",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("USA", Nationality.USA, "미국", 2, 0.72f, "북중미", new[]
            {
                "New York","Los Angeles","Chicago","Seattle","Portland","Atlanta","Dallas","Houston",
                "Kansas City","Columbus","Philadelphia","Orlando","Miami","Denver","Salt Lake","San Jose",
                "Minneapolis","Nashville","Cincinnati","Austin","Charlotte","St. Louis","San Diego","Washington",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),

            N("CAN", Nationality.CAN, "캐나다", 1, 0.58f, "북중미", new[]
            {
                "Toronto","Vancouver","Montreal","Calgary","Ottawa","Edmonton","Winnipeg","Hamilton",
                "Halifax","Victoria","Quebec","Saskatoon","Regina","London","Windsor","Kitchener",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),

            N("CRC", Nationality.CRC, "코스타리카", 1, 0.54f, "북중미", new[]
            {
                "San Jose","Alajuela","Cartago","Heredia","Liberia","Puntarenas","Limon","Perez Zeledon",
                "Santa Ana","Desamparados","Grecia","Turrialba","Guapiles","Nicoya","Quepos","San Carlos",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            // ── 아시아 ────────────────────────────────────────────────
            N("JPN", Nationality.JPN, "일본", 2, 0.74f, "아시아", new[]
            {
                "Tokyo","Osaka","Yokohama","Nagoya","Sapporo","Kobe","Fukuoka","Hiroshima",
                "Sendai","Kawasaki","Saitama","Chiba","Niigata","Shizuoka","Kashima","Urawa",
                "Kyoto","Okayama","Matsumoto","Oita","Kumamoto","Toyama","Kanazawa","Nagasaki",
            }, NonePre, AsiaSuf, StGen, NamePools.JapaneseFirst, NamePools.JapaneseLast),

            N("KOR", Nationality.KOR, "대한민국", 2, 0.72f, "아시아", new[]
            {
                "Seoul","Busan","Incheon","Daegu","Daejeon","Gwangju","Ulsan","Suwon",
                "Seongnam","Jeonju","Pohang","Changwon","Jeju","Gangwon","Gimcheon","Ansan",
                "Bucheon","Cheonan","Cheongju","Gyeongnam","Anyang","Mokpo","Yongin","Gimpo",
            }, NonePre, AsiaSuf, StGen, NamePools.KoreanFirst, NamePools.KoreanLast),

            N("CHN", Nationality.CHN, "중국", 1, 0.62f, "아시아", new[]
            {
                "Beijing","Shanghai","Guangzhou","Shenzhen","Chengdu","Wuhan","Tianjin","Chongqing",
                "Hangzhou","Nanjing","Qingdao","Dalian","Jinan","Changchun","Shenyang","Xiamen",
            }, NonePre, AsiaSuf, StGen, NamePools.ChineseFirst, NamePools.ChineseLast),

            N("KSA", Nationality.KSA, "사우디아라비아", 1, 0.72f, "아시아", new[]
            {
                "Riyadh","Jeddah","Dammam","Mecca","Medina","Taif","Buraidah","Khobar",
                "Abha","Hail","Najran","Jubail","Yanbu","Tabuk","Qatif","Hofuf",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("IRN", Nationality.IRN, "이란", 1, 0.62f, "아시아", new[]
            {
                "Tehran","Mashhad","Isfahan","Tabriz","Shiraz","Ahvaz","Qom","Kermanshah",
                "Rasht","Yazd","Karaj","Hamadan","Bandar Abbas","Arak","Zanjan","Sanandaj",
            }, NonePre, AsiaSuf, StGen, NamePools.PersianFirst, NamePools.PersianLast),

            N("AUS", Nationality.AUS, "호주", 1, 0.62f, "아시아", new[]
            {
                "Sydney","Melbourne","Brisbane","Perth","Adelaide","Newcastle","Wollongong","Canberra",
                "Gold Coast","Hobart","Darwin","Geelong","Townsville","Cairns","Ballarat","Bendigo",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),

            N("QAT", Nationality.QAT, "카타르", 1, 0.62f, "아시아", new[]
            {
                "Doha","Al Rayyan","Al Wakrah","Umm Salal","Al Khor","Lusail","Al Shamal","Al Daayen",
                "Mesaieed","Dukhan","Al Gharrafa","Al Sadd","Al Duhail","Al Sailiya","Al Markhiya","Qatar SC",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("UAE", Nationality.UAE, "아랍에미리트", 1, 0.62f, "아시아", new[]
            {
                "Dubai","Abu Dhabi","Sharjah","Al Ain","Ajman","Fujairah","Ras Al Khaimah","Umm Al Quwain",
                "Khor Fakkan","Dibba","Kalba","Madinat Zayed","Ruwais","Hatta","Jebel Ali","Masafi",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            // ── 아프리카 ──────────────────────────────────────────────
            N("EGY", Nationality.EGY, "이집트", 1, 0.64f, "아프리카", new[]
            {
                "Cairo","Alexandria","Giza","Port Said","Suez","Ismailia","Aswan","Luxor",
                "Mansoura","Tanta","Zagazig","Asyut","Fayoum","Damanhur","Minya","Beni Suef",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("MAR", Nationality.MAR, "모로코", 1, 0.64f, "아프리카", new[]
            {
                "Casablanca","Rabat","Marrakech","Fes","Tangier","Agadir","Meknes","Oujda",
                "Kenitra","Tetouan","Safi","El Jadida","Beni Mellal","Nador","Khouribga","Berkane",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("TUN", Nationality.TUN, "튀니지", 1, 0.58f, "아프리카", new[]
            {
                "Tunis","Sfax","Sousse","Bizerte","Gabes","Kairouan","Monastir","Nabeul",
                "Ben Arous","Gafsa","Medenine","Zarzis","Beja","Jendouba","Mahdia","Siliana",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("ALG", Nationality.ALG, "알제리", 1, 0.58f, "아프리카", new[]
            {
                "Alger","Oran","Constantine","Annaba","Blida","Setif","Batna","Tlemcen",
                "Bejaia","Tizi Ouzou","Chlef","Djelfa","Biskra","Skikda","Mostaganem","Relizane",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("NGA", Nationality.NGA, "나이지리아", 1, 0.56f, "아프리카", new[]
            {
                "Lagos","Abuja","Kano","Ibadan","Port Harcourt","Enugu","Kaduna","Benin City",
                "Jos","Calabar","Uyo","Warri","Aba","Owerri","Ilorin","Maiduguri",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("GHA", Nationality.GHA, "가나", 1, 0.54f, "아프리카", new[]
            {
                "Accra","Kumasi","Tamale","Sekondi","Cape Coast","Sunyani","Ho","Koforidua",
                "Techiman","Obuasi","Tema","Bolgatanga","Wa","Berekum","Dormaa","Nkawkaw",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("SEN", Nationality.SEN, "세네갈", 1, 0.56f, "아프리카", new[]
            {
                "Dakar","Thies","Kaolack","Saint-Louis","Ziguinchor","Diourbel","Louga","Tambacounda",
                "Mbour","Rufisque","Touba","Kolda","Fatick","Matam","Sedhiou","Kedougou",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("RSA", Nationality.RSA, "남아프리카공화국", 1, 0.58f, "아프리카", new[]
            {
                "Johannesburg","Cape Town","Durban","Pretoria","Port Elizabeth","Bloemfontein","Polokwane","Nelspruit",
                "Kimberley","Rustenburg","East London","Mbombela","Soweto","Pietermaritzburg","Witbank","Welkom",
            }, NonePre, AsiaSuf, StGen, NamePools.SouthAfricanFirst, NamePools.SouthAfricanLast),

            // ── 오세아니아 ────────────────────────────────────────────
            N("NZL", Nationality.NZL, "뉴질랜드", 1, 0.50f, "오세아니아", new[]
            {
                "Auckland","Wellington","Christchurch","Hamilton","Dunedin","Tauranga","Napier","Palmerston",
                "Rotorua","Nelson","Invercargill","New Plymouth","Whangarei","Gisborne","Timaru","Queenstown",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),
            // ── 2026-11-03 에 더한 나라들 ─────────────────────────────
            // 유럽
            N("ROU", Nationality.ROU, "루마니아", 1, 0.66f, "유럽", new[]
            {
                "Bucuresti","Cluj","Timisoara","Iasi","Constanta","Craiova","Galati","Brasov",
                "Ploiesti","Oradea","Arad","Sibiu","Targu Mures","Bacau","Pitesti","Botosani",
            }, NonePre, AsiaSuf, StGen, NamePools.RomanianFirst, NamePools.RomanianLast),

            N("HUN", Nationality.HUN, "헝가리", 1, 0.62f, "유럽", new[]
            {
                "Budapest","Debrecen","Szeged","Miskolc","Pecs","Gyor","Nyiregyhaza","Kecskemet",
                "Szekesfehervar","Szombathely","Sopron","Eger","Kaposvar","Veszprem","Zalaegerszeg","Paks",
            }, NonePre, AsiaSuf, StGen, NamePools.HungarianFirst, NamePools.HungarianLast),

            N("BUL", Nationality.BUL, "불가리아", 1, 0.60f, "유럽", new[]
            {
                "Sofia","Plovdiv","Varna","Burgas","Ruse","Stara Zagora","Pleven","Sliven",
                "Dobrich","Shumen","Pernik","Haskovo","Blagoevgrad","Veliko Tarnovo","Vratsa","Razgrad",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("SVK", Nationality.SVK, "슬로바키아", 1, 0.60f, "유럽", new[]
            {
                "Bratislava","Kosice","Zilina","Trnava","Nitra","Presov","Banska Bystrica","Trencin",
                "Dunajska Streda","Ruzomberok","Michalovce","Poprad","Martin","Komarno","Zvolen","Senica",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("SVN", Nationality.SVN, "슬로베니아", 1, 0.58f, "유럽", new[]
            {
                "Ljubljana","Maribor","Celje","Koper","Kranj","Velenje","Novo Mesto","Ptuj",
                "Murska Sobota","Nova Gorica","Domzale","Krsko","Sezana","Radomlje","Jesenice","Trbovlje",
            }, SlvPre, None, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("BIH", Nationality.BIH, "보스니아헤르체고비나", 1, 0.56f, "유럽", new[]
            {
                "Sarajevo","Banja Luka","Mostar","Tuzla","Zenica","Bijeljina","Prijedor","Trebinje",
                "Doboj","Travnik","Gorazde","Siroki Brijeg","Posusje","Cazin","Bihac","Livno",
            }, SlvPre, None, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("ALB", Nationality.ALB, "알바니아", 1, 0.52f, "유럽", new[]
            {
                "Tirana","Durres","Vlore","Shkoder","Elbasan","Korce","Fier","Berat",
                "Lushnje","Kukes","Lac","Kamza","Pogradec","Gjirokaster","Peshkopi","Sarande",
            }, NonePre, AsiaSuf, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("FIN", Nationality.FIN, "핀란드", 1, 0.56f, "유럽", new[]
            {
                "Helsinki","Tampere","Turku","Oulu","Lahti","Kuopio","Jyvaskyla","Pori",
                "Vaasa","Seinajoki","Rovaniemi","Kotka","Joensuu","Lappeenranta","Hameenlinna","Mikkeli",
            }, NorPre, None, StGen, NamePools.FinnishFirst, NamePools.FinnishLast),

            N("ISL", Nationality.ISL, "아이슬란드", 1, 0.50f, "유럽", new[]
            {
                "Reykjavik","Kopavogur","Hafnarfjordur","Akureyri","Keflavik","Gardabaer","Selfoss","Akranes",
                "Vestmannaeyjar","Isafjordur","Egilsstadir","Mosfellsbaer","Grindavik","Njardvik","Hveragerdi","Borgarnes",
            }, NorPre, None, StGen, NamePools.NordicFirst, NamePools.NordicLast),

            N("IRL", Nationality.IRL, "아일랜드", 1, 0.54f, "유럽", new[]
            {
                "Dublin","Cork","Galway","Limerick","Waterford","Drogheda","Dundalk","Sligo",
                "Athlone","Bray","Longford","Wexford","Kilkenny","Tralee","Cobh","Derry",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            N("WAL", Nationality.WAL, "웨일스", 1, 0.50f, "유럽", new[]
            {
                "Cardiff","Swansea","Newport","Wrexham","Bangor","Aberystwyth","Barry","Caernarfon",
                "Llanelli","Merthyr","Rhyl","Haverfordwest","Flint","Colwyn Bay","Caerphilly","Bridgend",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            N("CYP", Nationality.CYP, "키프로스", 1, 0.56f, "유럽", new[]
            {
                "Nicosia","Limassol","Larnaca","Paphos","Famagusta","Kyrenia","Paralimni","Aradippou",
                "Polis","Morphou","Lefka","Athienou","Dherynia","Ayia Napa","Pyla","Kiti",
            }, NonePre, AsiaSuf, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("ISR", Nationality.ISR, "이스라엘", 1, 0.62f, "유럽", new[]
            {
                "Tel Aviv","Jerusalem","Haifa","Beer Sheva","Netanya","Ashdod","Petah Tikva","Rishon LeZion",
                "Kiryat Shmona","Ashkelon","Herzliya","Ramat Gan","Hadera","Nazareth","Acre","Holon",
            }, NonePre, AsiaSuf, StGen, NamePools.HebrewFirst, NamePools.HebrewLast),

            N("KAZ", Nationality.KAZ, "카자흐스탄", 1, 0.56f, "유럽", new[]
            {
                "Astana","Almaty","Shymkent","Karaganda","Aktobe","Taraz","Pavlodar","Oskemen",
                "Kostanay","Atyrau","Aktau","Kyzylorda","Semey","Turkistan","Petropavl","Taldykorgan",
            }, SlvPre, None, StGen, NamePools.CentralAsianFirst, NamePools.CentralAsianLast),

            // 남아메리카
            N("ECU", Nationality.ECU, "에콰도르", 1, 0.60f, "남아메리카", new[]
            {
                "Quito","Guayaquil","Cuenca","Ambato","Manta","Machala","Portoviejo","Loja",
                "Riobamba","Esmeraldas","Ibarra","Santo Domingo","Quevedo","Milagro","Babahoyo","Latacunga",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("PAR", Nationality.PAR, "파라과이", 1, 0.58f, "남아메리카", new[]
            {
                "Asuncion","Ciudad del Este","San Lorenzo","Luque","Capiata","Lambare","Encarnacion","Villarrica",
                "Concepcion","Coronel Oviedo","Itaugua","Nemby","Caaguazu","Pilar","Paraguari","Ypacarai",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("BOL", Nationality.BOL, "볼리비아", 1, 0.52f, "남아메리카", new[]
            {
                "La Paz","Santa Cruz","Cochabamba","Oruro","Sucre","Potosi","Tarija","Trinidad",
                "Montero","Quillacollo","Riberalta","Yacuiba","El Alto","Camiri","Tupiza","Bermejo",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("VEN", Nationality.VEN, "베네수엘라", 1, 0.54f, "남아메리카", new[]
            {
                "Caracas","Maracaibo","Valencia","Barquisimeto","Maracay","Ciudad Guayana","Maturin","San Cristobal",
                "Merida","Cumana","Barinas","Puerto La Cruz","Acarigua","Punto Fijo","Coro","Guanare",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            // 북중미
            N("HON", Nationality.HON, "온두라스", 1, 0.52f, "북중미", new[]
            {
                "Tegucigalpa","San Pedro Sula","La Ceiba","Choluteca","Comayagua","Puerto Cortes","Danli","Siguatepeque",
                "Juticalpa","Tela","Olanchito","Santa Rosa","Progreso","Villanueva","Choloma","Catacamas",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("GUA", Nationality.GUA, "과테말라", 1, 0.52f, "북중미", new[]
            {
                "Guatemala","Quetzaltenango","Escuintla","Mixco","Villa Nueva","Coban","Antigua","Retalhuleu",
                "Mazatenango","Chiquimula","Jalapa","Puerto Barrios","Zacapa","Solola","Huehuetenango","Peten",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("PAN", Nationality.PAN, "파나마", 1, 0.50f, "북중미", new[]
            {
                "Panama","Colon","David","Santiago","Chitre","La Chorrera","Penonome","Aguadulce",
                "Changuinola","Las Tablas","Bocas del Toro","Arraijan","Pedasi","Boquete","Chepo","Anton",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("JAM", Nationality.JAM, "자메이카", 1, 0.52f, "북중미", new[]
            {
                "Kingston","Montego Bay","Spanish Town","Portmore","Mandeville","May Pen","Ocho Rios","Negril",
                "Savanna-la-Mar","Port Antonio","Falmouth","Linstead","Old Harbour","Morant Bay","Lucea","Black River",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            // 아시아
            N("IND", Nationality.IND, "인도", 1, 0.58f, "아시아", new[]
            {
                "Mumbai","Kolkata","Bengaluru","Chennai","Hyderabad","Pune","Goa","Kochi",
                "Jamshedpur","Guwahati","Ahmedabad","Delhi","Imphal","Bhubaneswar","Jaipur","Ludhiana",
            }, NonePre, AsiaSuf, StGen, NamePools.IndianFirst, NamePools.IndianLast),

            N("THA", Nationality.THA, "태국", 1, 0.60f, "아시아", new[]
            {
                "Bangkok","Chiang Mai","Buriram","Chonburi","Pattaya","Khon Kaen","Nakhon Ratchasima","Songkhla",
                "Rayong","Suphanburi","Ratchaburi","Udon Thani","Phuket","Lampang","Trat","Nonthaburi",
            }, NonePre, AsiaSuf, StGen, NamePools.ThaiFirst, NamePools.ThaiLast),

            N("VIE", Nationality.VIE, "베트남", 1, 0.56f, "아시아", new[]
            {
                "Hanoi","Ho Chi Minh","Da Nang","Hai Phong","Can Tho","Nam Dinh","Thanh Hoa","Nghe An",
                "Binh Duong","Khanh Hoa","Quang Ninh","Hue","Dong Thap","Long An","Gia Lai","Hai Duong",
            }, NonePre, AsiaSuf, StGen, NamePools.VietnameseFirst, NamePools.VietnameseLast),

            N("IDN", Nationality.IDN, "인도네시아", 1, 0.56f, "아시아", new[]
            {
                "Jakarta","Surabaya","Bandung","Medan","Semarang","Makassar","Palembang","Yogyakarta",
                "Malang","Solo","Bali","Balikpapan","Pontianak","Manado","Padang","Banjarmasin",
            }, NonePre, AsiaSuf, StGen, NamePools.MalayFirst, NamePools.MalayLast),

            N("MAS", Nationality.MAS, "말레이시아", 1, 0.54f, "아시아", new[]
            {
                "Kuala Lumpur","Johor Bahru","Penang","Ipoh","Kuching","Kota Kinabalu","Melaka","Kuantan",
                "Alor Setar","Shah Alam","Seremban","Kota Bharu","Kuala Terengganu","Sandakan","Miri","Sibu",
            }, NonePre, AsiaSuf, StGen, NamePools.MalayFirst, NamePools.MalayLast),

            N("SIN", Nationality.SIN, "싱가포르", 1, 0.50f, "아시아", new[]
            {
                "Tampines","Jurong","Woodlands","Hougang","Bedok","Yishun","Clementi","Geylang",
                "Bishan","Tanjong Pagar","Serangoon","Bukit Batok","Choa Chu Kang","Punggol","Sembawang","Queenstown",
            }, NonePre, AsiaSuf, StGen, NamePools.ChineseFirst, NamePools.ChineseLast),

            N("UZB", Nationality.UZB, "우즈베키스탄", 1, 0.56f, "아시아", new[]
            {
                "Tashkent","Samarkand","Namangan","Andijan","Bukhara","Nukus","Fergana","Qarshi",
                "Jizzakh","Navoi","Termez","Urgench","Kokand","Margilan","Angren","Chirchiq",
            }, NonePre, AsiaSuf, StGen, NamePools.CentralAsianFirst, NamePools.CentralAsianLast),

            N("IRQ", Nationality.IRQ, "이라크", 1, 0.54f, "아시아", new[]
            {
                "Baghdad","Basra","Mosul","Erbil","Najaf","Karbala","Kirkuk","Sulaymaniyah",
                "Nasiriyah","Amarah","Diwaniyah","Hillah","Ramadi","Samarra","Duhok","Kut",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("JOR", Nationality.JOR, "요르단", 1, 0.52f, "아시아", new[]
            {
                "Amman","Zarqa","Irbid","Aqaba","Salt","Madaba","Mafraq","Jerash",
                "Karak","Tafilah","Maan","Ajloun","Ramtha","Russeifa","Wadi Musa","Azraq",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            // 아프리카
            N("CIV", Nationality.CIV, "코트디부아르", 1, 0.54f, "아프리카", new[]
            {
                "Abidjan","Bouake","Yamoussoukro","San Pedro","Korhogo","Daloa","Man","Gagnoa",
                "Divo","Abengourou","Anyama","Soubre","Agboville","Grand-Bassam","Bondoukou","Sassandra",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("CMR", Nationality.CMR, "카메룬", 1, 0.54f, "아프리카", new[]
            {
                "Douala","Yaounde","Garoua","Bamenda","Bafoussam","Maroua","Ngaoundere","Kumba",
                "Buea","Limbe","Edea","Kribi","Dschang","Ebolowa","Bertoua","Foumban",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("COD", Nationality.COD, "콩고민주공화국", 1, 0.52f, "아프리카", new[]
            {
                "Kinshasa","Lubumbashi","Mbuji-Mayi","Kananga","Kisangani","Bukavu","Goma","Likasi",
                "Kolwezi","Tshikapa","Matadi","Mbandaka","Uvira","Butembo","Bunia","Kindu",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("MLI", Nationality.MLI, "말리", 1, 0.50f, "아프리카", new[]
            {
                "Bamako","Sikasso","Mopti","Segou","Kayes","Koutiala","Gao","Timbuktu",
                "Kati","Djenne","San","Bougouni","Nioro","Kidal","Markala","Niono",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("ZAM", Nationality.ZAM, "잠비아", 1, 0.50f, "아프리카", new[]
            {
                "Lusaka","Kitwe","Ndola","Kabwe","Chingola","Mufulira","Livingstone","Luanshya",
                "Kasama","Chipata","Solwezi","Mazabuka","Choma","Mongu","Kalulushi","Mansa",
            }, NonePre, AsiaSuf, StGen, NamePools.EastAfricanFirst, NamePools.EastAfricanLast),

            N("ANG", Nationality.ANG, "앙골라", 1, 0.52f, "아프리카", new[]
            {
                "Luanda","Huambo","Lobito","Benguela","Lubango","Malanje","Namibe","Cabinda",
                "Soyo","Uige","Kuito","Saurimo","Menongue","Sumbe","Ndalatando","Caxito",
            }, PorPre, None, StGen, NamePools.PortugueseFirst, NamePools.PortugueseLast),

            N("KEN", Nationality.KEN, "케냐", 1, 0.50f, "아프리카", new[]
            {
                "Nairobi","Mombasa","Kisumu","Nakuru","Eldoret","Thika","Machakos","Kakamega",
                "Nyeri","Kericho","Meru","Garissa","Kitale","Malindi","Embu","Bungoma",
            }, NonePre, AsiaSuf, StGen, NamePools.EastAfricanFirst, NamePools.EastAfricanLast),

            // 오세아니아
            N("FIJ", Nationality.FIJ, "피지", 1, 0.44f, "오세아니아", new[]
            {
                "Suva","Lautoka","Nadi","Labasa","Ba","Sigatoka","Levuka","Savusavu",
                "Nausori","Rakiraki","Tavua","Navua","Korovou","Vatukoula","Taveuni","Rewa",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),

            N("PNG", Nationality.PNG, "파푸아뉴기니", 1, 0.44f, "오세아니아", new[]
            {
                "Port Moresby","Lae","Madang","Mount Hagen","Goroka","Kokopo","Wewak","Kimbe",
                "Popondetta","Alotau","Kavieng","Daru","Mendi","Vanimo","Lorengau","Kundiawa",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),
        };

    }
}
