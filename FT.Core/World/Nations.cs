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

            N("POR", Nationality.POR, "포르투갈", 3, 0.84f, "유럽", new[]
            {
                "Porto","Braga","Guimaraes","Coimbra","Setubal","Faro","Aveiro","Leiria",
                "Viseu","Funchal","Portimao","Chaves","Famalicao","Moreira","Estoril","Barcelos",
                "Covilha","Tondela","Penafiel","Mafra",
            }, PorPre, None, StGen, NamePools.PortugueseFirst, NamePools.PortugueseLast),

            N("BEL", Nationality.BEL, "벨기에", 3, 0.78f, "유럽", new[]
            {
                "Brugge","Antwerpen","Gent","Luik","Charleroi","Genk","Mechelen","Leuven",
                "Kortrijk","Oostende","Hasselt","Namen","Eupen","Waregem","Dender","Lokeren",
                "Beveren","Westerlo","Seraing","Virton",
            }, NedPre, None, StGen, NamePools.DutchFirst, NamePools.DutchLast),

            N("TUR", Nationality.TUR, "튀르키예", 3, 0.78f, "유럽", new[]
            {
                "Istanbul","Ankara","Izmir","Bursa","Antalya","Adana","Konya","Trabzon",
                "Kayseri","Gaziantep","Samsun","Eskisehir","Denizli","Malatya","Sivas","Rize",
                "Erzurum","Mersin","Diyarbakir","Balikesir",
            }, NonePre, AsiaSuf, StGen, NamePools.TurkishFirst, NamePools.TurkishLast),

            N("SCO", Nationality.SCO, "스코틀랜드", 3, 0.70f, "유럽", new[]
            {
                "Glasgow","Edinburgh","Aberdeen","Dundee","Perth","Inverness","Motherwell","Kilmarnock",
                "Paisley","Falkirk","Ayr","Dunfermline","Livingston","Hamilton","Greenock","Stirling",
                "Elgin","Montrose","Arbroath","Alloa",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            N("RUS", Nationality.RUS, "러시아", 3, 0.76f, "유럽", new[]
            {
                "Moskva","Sankt-Peterburg","Kazan","Samara","Rostov","Krasnodar","Yekaterinburg","Novosibirsk",
                "Volgograd","Voronezh","Perm","Ufa","Sochi","Tula","Orenburg","Khimki",
                "Nizhny","Omsk","Chelyabinsk","Yaroslavl",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("UKR", Nationality.UKR, "우크라이나", 3, 0.70f, "유럽", new[]
            {
                "Kyiv","Lviv","Odesa","Kharkiv","Dnipro","Donetsk","Zaporizhzhia","Poltava",
                "Vinnytsia","Mariupol","Luhansk","Chernivtsi","Rivne","Ternopil","Kovalivka","Uzhhorod",
                "Kryvyi Rih","Mykolaiv","Sumy","Cherkasy",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("AUT", Nationality.AUT, "오스트리아", 2, 0.70f, "유럽", new[]
            {
                "Wien","Salzburg","Graz","Linz","Innsbruck","Klagenfurt","Wolfsberg","Ried",
                "Altach","Hartberg","St. Polten","Mattersburg","Wiener Neustadt","Villach","Bregenz","Dornbirn",
            }, GerPre, None, StGer, NamePools.GermanFirst, NamePools.GermanLast),

            N("SUI", Nationality.SUI, "스위스", 2, 0.70f, "유럽", new[]
            {
                "Zurich","Basel","Bern","Geneve","Lausanne","Luzern","St. Gallen","Lugano",
                "Sion","Winterthur","Thun","Aarau","Biel","Chiasso","Vaduz","Schaffhausen",
            }, GerPre, None, StGer, NamePools.GermanFirst, NamePools.GermanLast),

            N("DEN", Nationality.DEN, "덴마크", 3, 0.70f, "유럽", new[]
            {
                "Kobenhavn","Aarhus","Odense","Aalborg","Esbjerg","Randers","Horsens","Vejle",
                "Silkeborg","Brondby","Lyngby","Viborg","Sonderborg","Herning","Naestved","Hvidovre",
            }, NorPre, None, StGen, NamePools.NordicFirst, NamePools.NordicLast),

            N("SWE", Nationality.SWE, "스웨덴", 3, 0.64f, "유럽", new[]
            {
                "Stockholm","Goteborg","Malmo","Uppsala","Norrkoping","Helsingborg","Orebro","Vasteras",
                "Sundsvall","Kalmar","Halmstad","Jonkoping","Umea","Gavle","Linkoping","Boras",
            }, NorPre, None, StGen, NamePools.NordicFirst, NamePools.NordicLast),

            N("NOR", Nationality.NOR, "노르웨이", 3, 0.64f, "유럽", new[]
            {
                "Oslo","Bergen","Trondheim","Stavanger","Kristiansand","Tromso","Drammen","Bodo",
                "Sarpsborg","Haugesund","Lillestrom","Molde","Sandefjord","Alesund","Hamar","Fredrikstad",
            }, NorPre, None, StGen, NamePools.NordicFirst, NamePools.NordicLast),

            N("POL", Nationality.POL, "폴란드", 3, 0.66f, "유럽", new[]
            {
                "Warszawa","Krakow","Lodz","Wroclaw","Poznan","Gdansk","Szczecin","Lublin",
                "Katowice","Bialystok","Czestochowa","Gdynia","Plock","Legnica","Zabrze","Lubin",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("GRE", Nationality.GRE, "그리스", 2, 0.70f, "유럽", new[]
            {
                "Athina","Thessaloniki","Patra","Larissa","Volos","Iraklio","Ioannina","Serres",
                "Kavala","Xanthi","Rodos","Chania","Trikala","Lamia","Kalamata","Agrinio",
            }, NonePre, AsiaSuf, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("CZE", Nationality.CZE, "체코", 2, 0.70f, "유럽", new[]
            {
                "Praha","Brno","Ostrava","Plzen","Olomouc","Liberec","Hradec","Pardubice",
                "Zlin","Teplice","Jablonec","Mlada Boleslav","Karvina","Budejovice","Opava","Slovacko",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("SRB", Nationality.SRB, "세르비아", 2, 0.64f, "유럽", new[]
            {
                "Beograd","Novi Sad","Nis","Kragujevac","Subotica","Cacak","Krusevac","Zrenjanin",
                "Smederevo","Pancevo","Leskovac","Vranje","Sombor","Valjevo","Uzice","Sabac",
            }, SlvPre, None, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("CRO", Nationality.CRO, "크로아티아", 2, 0.66f, "유럽", new[]
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

            N("URU", Nationality.URU, "우루과이", 2, 0.62f, "남아메리카", new[]
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

            N("CHI", Nationality.CHI, "칠레", 2, 0.62f, "남아메리카", new[]
            {
                "Santiago","Valparaiso","Concepcion","Antofagasta","Temuco","Iquique","La Serena","Rancagua",
                "Talca","Puerto Montt","Calama","Arica","Chillan","Osorno","Valdivia","Copiapo",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("PER", Nationality.PER, "페루", 2, 0.58f, "남아메리카", new[]
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

            N("USA", Nationality.USA, "미국", 3, 0.72f, "북중미", new[]
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

            N("CRC", Nationality.CRC, "코스타리카", 2, 0.54f, "북중미", new[]
            {
                "San Jose","Alajuela","Cartago","Heredia","Liberia","Puntarenas","Limon","Perez Zeledon",
                "Santa Ana","Desamparados","Grecia","Turrialba","Guapiles","Nicoya","Quepos","San Carlos",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            // ── 아시아 ────────────────────────────────────────────────
            N("JPN", Nationality.JPN, "일본", 3, 0.74f, "아시아", new[]
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

            N("CHN", Nationality.CHN, "중국", 3, 0.62f, "아시아", new[]
            {
                "Beijing","Shanghai","Guangzhou","Shenzhen","Chengdu","Wuhan","Tianjin","Chongqing",
                "Hangzhou","Nanjing","Qingdao","Dalian","Jinan","Changchun","Shenyang","Xiamen",
            }, NonePre, AsiaSuf, StGen, NamePools.ChineseFirst, NamePools.ChineseLast),

            N("KSA", Nationality.KSA, "사우디아라비아", 3, 0.72f, "아시아", new[]
            {
                "Riyadh","Jeddah","Dammam","Mecca","Medina","Taif","Buraidah","Khobar",
                "Abha","Hail","Najran","Jubail","Yanbu","Tabuk","Qatif","Hofuf",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("IRN", Nationality.IRN, "이란", 3, 0.62f, "아시아", new[]
            {
                "Tehran","Mashhad","Isfahan","Tabriz","Shiraz","Ahvaz","Qom","Kermanshah",
                "Rasht","Yazd","Karaj","Hamadan","Bandar Abbas","Arak","Zanjan","Sanandaj",
            }, NonePre, AsiaSuf, StGen, NamePools.PersianFirst, NamePools.PersianLast),

            N("AUS", Nationality.AUS, "호주", 1, 0.62f, "아시아", new[]
            {
                "Sydney","Melbourne","Brisbane","Perth","Adelaide","Newcastle","Wollongong","Canberra",
                "Gold Coast","Hobart","Darwin","Geelong","Townsville","Cairns","Ballarat","Bendigo",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),

            N("QAT", Nationality.QAT, "카타르", 2, 0.62f, "아시아", new[]
            {
                "Doha","Al Rayyan","Al Wakrah","Umm Salal","Al Khor","Lusail","Al Shamal","Al Daayen",
                "Mesaieed","Dukhan","Al Gharrafa","Al Sadd","Al Duhail","Al Sailiya","Al Markhiya","Qatar SC",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("UAE", Nationality.UAE, "아랍에미리트", 2, 0.62f, "아시아", new[]
            {
                "Dubai","Abu Dhabi","Sharjah","Al Ain","Ajman","Fujairah","Ras Al Khaimah","Umm Al Quwain",
                "Khor Fakkan","Dibba","Kalba","Madinat Zayed","Ruwais","Hatta","Jebel Ali","Masafi",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            // ── 아프리카 ──────────────────────────────────────────────
            N("EGY", Nationality.EGY, "이집트", 2, 0.64f, "아프리카", new[]
            {
                "Cairo","Alexandria","Giza","Port Said","Suez","Ismailia","Aswan","Luxor",
                "Mansoura","Tanta","Zagazig","Asyut","Fayoum","Damanhur","Minya","Beni Suef",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("MAR", Nationality.MAR, "모로코", 2, 0.64f, "아프리카", new[]
            {
                "Casablanca","Rabat","Marrakech","Fes","Tangier","Agadir","Meknes","Oujda",
                "Kenitra","Tetouan","Safi","El Jadida","Beni Mellal","Nador","Khouribga","Berkane",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("TUN", Nationality.TUN, "튀니지", 2, 0.58f, "아프리카", new[]
            {
                "Tunis","Sfax","Sousse","Bizerte","Gabes","Kairouan","Monastir","Nabeul",
                "Ben Arous","Gafsa","Medenine","Zarzis","Beja","Jendouba","Mahdia","Siliana",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("ALG", Nationality.ALG, "알제리", 2, 0.58f, "아프리카", new[]
            {
                "Alger","Oran","Constantine","Annaba","Blida","Setif","Batna","Tlemcen",
                "Bejaia","Tizi Ouzou","Chlef","Djelfa","Biskra","Skikda","Mostaganem","Relizane",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("NGA", Nationality.NGA, "나이지리아", 2, 0.56f, "아프리카", new[]
            {
                "Lagos","Abuja","Kano","Ibadan","Port Harcourt","Enugu","Kaduna","Benin City",
                "Jos","Calabar","Uyo","Warri","Aba","Owerri","Ilorin","Maiduguri",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("GHA", Nationality.GHA, "가나", 2, 0.54f, "아프리카", new[]
            {
                "Accra","Kumasi","Tamale","Sekondi","Cape Coast","Sunyani","Ho","Koforidua",
                "Techiman","Obuasi","Tema","Bolgatanga","Wa","Berekum","Dormaa","Nkawkaw",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("SEN", Nationality.SEN, "세네갈", 1, 0.56f, "아프리카", new[]
            {
                "Dakar","Thies","Kaolack","Saint-Louis","Ziguinchor","Diourbel","Louga","Tambacounda",
                "Mbour","Rufisque","Touba","Kolda","Fatick","Matam","Sedhiou","Kedougou",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("RSA", Nationality.RSA, "남아프리카공화국", 2, 0.58f, "아프리카", new[]
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
            N("ROU", Nationality.ROU, "루마니아", 3, 0.66f, "유럽", new[]
            {
                "Bucuresti","Cluj","Timisoara","Iasi","Constanta","Craiova","Galati","Brasov",
                "Ploiesti","Oradea","Arad","Sibiu","Targu Mures","Bacau","Pitesti","Botosani",
            }, NonePre, AsiaSuf, StGen, NamePools.RomanianFirst, NamePools.RomanianLast),

            N("HUN", Nationality.HUN, "헝가리", 3, 0.62f, "유럽", new[]
            {
                "Budapest","Debrecen","Szeged","Miskolc","Pecs","Gyor","Nyiregyhaza","Kecskemet",
                "Szekesfehervar","Szombathely","Sopron","Eger","Kaposvar","Veszprem","Zalaegerszeg","Paks",
            }, NonePre, AsiaSuf, StGen, NamePools.HungarianFirst, NamePools.HungarianLast),

            N("BUL", Nationality.BUL, "불가리아", 2, 0.60f, "유럽", new[]
            {
                "Sofia","Plovdiv","Varna","Burgas","Ruse","Stara Zagora","Pleven","Sliven",
                "Dobrich","Shumen","Pernik","Haskovo","Blagoevgrad","Veliko Tarnovo","Vratsa","Razgrad",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("SVK", Nationality.SVK, "슬로바키아", 2, 0.60f, "유럽", new[]
            {
                "Bratislava","Kosice","Zilina","Trnava","Nitra","Presov","Banska Bystrica","Trencin",
                "Dunajska Streda","Ruzomberok","Michalovce","Poprad","Martin","Komarno","Zvolen","Senica",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("SVN", Nationality.SVN, "슬로베니아", 2, 0.58f, "유럽", new[]
            {
                "Ljubljana","Maribor","Celje","Koper","Kranj","Velenje","Novo Mesto","Ptuj",
                "Murska Sobota","Nova Gorica","Domzale","Krsko","Sezana","Radomlje","Jesenice","Trbovlje",
            }, SlvPre, None, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("BIH", Nationality.BIH, "보스니아헤르체고비나", 2, 0.56f, "유럽", new[]
            {
                "Sarajevo","Banja Luka","Mostar","Tuzla","Zenica","Bijeljina","Prijedor","Trebinje",
                "Doboj","Travnik","Gorazde","Siroki Brijeg","Posusje","Cazin","Bihac","Livno",
            }, SlvPre, None, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("ALB", Nationality.ALB, "알바니아", 2, 0.52f, "유럽", new[]
            {
                "Tirana","Durres","Vlore","Shkoder","Elbasan","Korce","Fier","Berat",
                "Lushnje","Kukes","Lac","Kamza","Pogradec","Gjirokaster","Peshkopi","Sarande",
            }, NonePre, AsiaSuf, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("FIN", Nationality.FIN, "핀란드", 3, 0.56f, "유럽", new[]
            {
                "Helsinki","Tampere","Turku","Oulu","Lahti","Kuopio","Jyvaskyla","Pori",
                "Vaasa","Seinajoki","Rovaniemi","Kotka","Joensuu","Lappeenranta","Hameenlinna","Mikkeli",
            }, NorPre, None, StGen, NamePools.FinnishFirst, NamePools.FinnishLast),

            N("ISL", Nationality.ISL, "아이슬란드", 3, 0.50f, "유럽", new[]
            {
                "Reykjavik","Kopavogur","Hafnarfjordur","Akureyri","Keflavik","Gardabaer","Selfoss","Akranes",
                "Vestmannaeyjar","Isafjordur","Egilsstadir","Mosfellsbaer","Grindavik","Njardvik","Hveragerdi","Borgarnes",
            }, NorPre, None, StGen, NamePools.NordicFirst, NamePools.NordicLast),

            N("IRL", Nationality.IRL, "아일랜드", 2, 0.54f, "유럽", new[]
            {
                "Dublin","Cork","Galway","Limerick","Waterford","Drogheda","Dundalk","Sligo",
                "Athlone","Bray","Longford","Wexford","Kilkenny","Tralee","Cobh","Derry",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            N("WAL", Nationality.WAL, "웨일스", 2, 0.50f, "유럽", new[]
            {
                "Cardiff","Swansea","Newport","Wrexham","Bangor","Aberystwyth","Barry","Caernarfon",
                "Llanelli","Merthyr","Rhyl","Haverfordwest","Flint","Colwyn Bay","Caerphilly","Bridgend",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            N("CYP", Nationality.CYP, "키프로스", 3, 0.56f, "유럽", new[]
            {
                "Nicosia","Limassol","Larnaca","Paphos","Famagusta","Kyrenia","Paralimni","Aradippou",
                "Polis","Morphou","Lefka","Athienou","Dherynia","Ayia Napa","Pyla","Kiti",
            }, NonePre, AsiaSuf, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("ISR", Nationality.ISR, "이스라엘", 3, 0.62f, "유럽", new[]
            {
                "Tel Aviv","Jerusalem","Haifa","Beer Sheva","Netanya","Ashdod","Petah Tikva","Rishon LeZion",
                "Kiryat Shmona","Ashkelon","Herzliya","Ramat Gan","Hadera","Nazareth","Acre","Holon",
            }, NonePre, AsiaSuf, StGen, NamePools.HebrewFirst, NamePools.HebrewLast),

            N("KAZ", Nationality.KAZ, "카자흐스탄", 2, 0.56f, "유럽", new[]
            {
                "Astana","Almaty","Shymkent","Karaganda","Aktobe","Taraz","Pavlodar","Oskemen",
                "Kostanay","Atyrau","Aktau","Kyzylorda","Semey","Turkistan","Petropavl","Taldykorgan",
            }, SlvPre, None, StGen, NamePools.CentralAsianFirst, NamePools.CentralAsianLast),

            // 남아메리카
            N("ECU", Nationality.ECU, "에콰도르", 2, 0.60f, "남아메리카", new[]
            {
                "Quito","Guayaquil","Cuenca","Ambato","Manta","Machala","Portoviejo","Loja",
                "Riobamba","Esmeraldas","Ibarra","Santo Domingo","Quevedo","Milagro","Babahoyo","Latacunga",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("PAR", Nationality.PAR, "파라과이", 2, 0.58f, "남아메리카", new[]
            {
                "Asuncion","Ciudad del Este","San Lorenzo","Luque","Capiata","Lambare","Encarnacion","Villarrica",
                "Concepcion","Coronel Oviedo","Itaugua","Nemby","Caaguazu","Pilar","Paraguari","Ypacarai",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("BOL", Nationality.BOL, "볼리비아", 1, 0.52f, "남아메리카", new[]
            {
                "La Paz","Santa Cruz","Cochabamba","Oruro","Sucre","Potosi","Tarija","Trinidad",
                "Montero","Quillacollo","Riberalta","Yacuiba","El Alto","Camiri","Tupiza","Bermejo",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("VEN", Nationality.VEN, "베네수엘라", 2, 0.54f, "남아메리카", new[]
            {
                "Caracas","Maracaibo","Valencia","Barquisimeto","Maracay","Ciudad Guayana","Maturin","San Cristobal",
                "Merida","Cumana","Barinas","Puerto La Cruz","Acarigua","Punto Fijo","Coro","Guanare",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            // 북중미
            N("HON", Nationality.HON, "온두라스", 2, 0.52f, "북중미", new[]
            {
                "Tegucigalpa","San Pedro Sula","La Ceiba","Choluteca","Comayagua","Puerto Cortes","Danli","Siguatepeque",
                "Juticalpa","Tela","Olanchito","Santa Rosa","Progreso","Villanueva","Choloma","Catacamas",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("GUA", Nationality.GUA, "과테말라", 2, 0.52f, "북중미", new[]
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
            N("IND", Nationality.IND, "인도", 2, 0.58f, "아시아", new[]
            {
                "Mumbai","Kolkata","Bengaluru","Chennai","Hyderabad","Pune","Goa","Kochi",
                "Jamshedpur","Guwahati","Ahmedabad","Delhi","Imphal","Bhubaneswar","Jaipur","Ludhiana",
            }, NonePre, AsiaSuf, StGen, NamePools.IndianFirst, NamePools.IndianLast),

            N("THA", Nationality.THA, "태국", 3, 0.60f, "아시아", new[]
            {
                "Bangkok","Chiang Mai","Buriram","Chonburi","Pattaya","Khon Kaen","Nakhon Ratchasima","Songkhla",
                "Rayong","Suphanburi","Ratchaburi","Udon Thani","Phuket","Lampang","Trat","Nonthaburi",
            }, NonePre, AsiaSuf, StGen, NamePools.ThaiFirst, NamePools.ThaiLast),

            N("VIE", Nationality.VIE, "베트남", 2, 0.56f, "아시아", new[]
            {
                "Hanoi","Ho Chi Minh","Da Nang","Hai Phong","Can Tho","Nam Dinh","Thanh Hoa","Nghe An",
                "Binh Duong","Khanh Hoa","Quang Ninh","Hue","Dong Thap","Long An","Gia Lai","Hai Duong",
            }, NonePre, AsiaSuf, StGen, NamePools.VietnameseFirst, NamePools.VietnameseLast),

            N("IDN", Nationality.IDN, "인도네시아", 2, 0.56f, "아시아", new[]
            {
                "Jakarta","Surabaya","Bandung","Medan","Semarang","Makassar","Palembang","Yogyakarta",
                "Malang","Solo","Bali","Balikpapan","Pontianak","Manado","Padang","Banjarmasin",
            }, NonePre, AsiaSuf, StGen, NamePools.MalayFirst, NamePools.MalayLast),

            N("MAS", Nationality.MAS, "말레이시아", 2, 0.54f, "아시아", new[]
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

            N("IRQ", Nationality.IRQ, "이라크", 2, 0.54f, "아시아", new[]
            {
                "Baghdad","Basra","Mosul","Erbil","Najaf","Karbala","Kirkuk","Sulaymaniyah",
                "Nasiriyah","Amarah","Diwaniyah","Hillah","Ramadi","Samarra","Duhok","Kut",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("JOR", Nationality.JOR, "요르단", 2, 0.52f, "아시아", new[]
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
            // ── 2026-11-03 (3차) 유럽 소국 ───────────────────────────
            N("GEO", Nationality.GEO, "조지아", 1, 0.54f, "유럽", new[]
            {
                "Tbilisi","Batumi","Kutaisi","Rustavi","Gori","Zugdidi","Poti","Telavi",
                "Akhaltsikhe","Ozurgeti","Senaki","Marneuli","Kaspi","Khashuri","Samtredia","Zestaponi",
            }, NonePre, AsiaSuf, StGen, NamePools.GeorgianFirst, NamePools.GeorgianLast),

            N("ARM", Nationality.ARM, "아르메니아", 1, 0.50f, "유럽", new[]
            {
                "Yerevan","Gyumri","Vanadzor","Abovyan","Kapan","Hrazdan","Armavir","Artashat",
                "Ijevan","Goris","Sevan","Ashtarak","Masis","Charentsavan","Dilijan","Echmiadzin",
            }, NonePre, AsiaSuf, StGen, NamePools.ArmenianFirst, NamePools.ArmenianLast),

            N("AZE", Nationality.AZE, "아제르바이잔", 1, 0.54f, "유럽", new[]
            {
                "Baku","Ganja","Sumqayit","Mingachevir","Lankaran","Shirvan","Nakhchivan","Shaki",
                "Yevlakh","Khirdalan","Barda","Quba","Salyan","Zaqatala","Agdam","Goychay",
            }, NonePre, AsiaSuf, StGen, NamePools.TurkishFirst, NamePools.TurkishLast),

            N("BLR", Nationality.BLR, "벨라루스", 1, 0.54f, "유럽", new[]
            {
                "Minsk","Gomel","Mogilev","Vitebsk","Grodno","Brest","Babruysk","Baranovichi",
                "Borisov","Pinsk","Orsha","Mozyr","Soligorsk","Novopolotsk","Lida","Molodechno",
            }, SlvPre, None, StGen, NamePools.SlavicFirst, NamePools.SlavicLast),

            N("MDA", Nationality.MDA, "몰도바", 1, 0.48f, "유럽", new[]
            {
                "Chisinau","Tiraspol","Balti","Bender","Ribnita","Cahul","Ungheni","Soroca",
                "Orhei","Comrat","Edinet","Causeni","Straseni","Hincesti","Drochia","Falesti",
            }, SlvPre, None, StGen, NamePools.RomanianFirst, NamePools.RomanianLast),

            N("LVA", Nationality.LVA, "라트비아", 1, 0.48f, "유럽", new[]
            {
                "Riga","Daugavpils","Liepaja","Jelgava","Jurmala","Ventspils","Rezekne","Valmiera",
                "Ogre","Jekabpils","Tukums","Salaspils","Cesis","Kuldiga","Saldus","Talsi",
            }, NorPre, None, StGen, NamePools.BalticFirst, NamePools.BalticLast),

            N("LTU", Nationality.LTU, "리투아니아", 1, 0.48f, "유럽", new[]
            {
                "Vilnius","Kaunas","Klaipeda","Siauliai","Panevezys","Alytus","Marijampole","Mazeikiai",
                "Jonava","Utena","Kedainiai","Telsiai","Taurage","Ukmerge","Visaginas","Plunge",
            }, NorPre, None, StGen, NamePools.BalticFirst, NamePools.BalticLast),

            N("EST", Nationality.EST, "에스토니아", 1, 0.48f, "유럽", new[]
            {
                "Tallinn","Tartu","Narva","Parnu","Kohtla-Jarve","Viljandi","Rakvere","Maardu",
                "Sillamae","Kuressaare","Valga","Voru","Haapsalu","Johvi","Paide","Keila",
            }, NorPre, None, StGen, NamePools.BalticFirst, NamePools.BalticLast),

            N("LUX", Nationality.LUX, "룩셈부르크", 1, 0.44f, "유럽", new[]
            {
                "Luxembourg","Esch-sur-Alzette","Differdange","Dudelange","Ettelbruck","Diekirch","Wiltz","Echternach",
                "Rumelange","Grevenmacher","Remich","Vianden","Bettembourg","Petange","Strassen","Hesperange",
            }, FraPre, None, StFra, NamePools.GermanFirst, NamePools.GermanLast),

            N("MLT", Nationality.MLT, "몰타", 1, 0.44f, "유럽", new[]
            {
                "Valletta","Birkirkara","Sliema","Qormi","Mosta","Zabbar","Hamrun","Rabat",
                "Naxxar","Zejtun","Gzira","Marsa","Victoria","Siggiewi","Mellieha","Zurrieq",
            }, NonePre, AsiaSuf, StGen, NamePools.ItalianFirst, NamePools.ItalianLast),

            N("MNE", Nationality.MNE, "몬테네그로", 1, 0.48f, "유럽", new[]
            {
                "Podgorica","Niksic","Pljevlja","Bijelo Polje","Cetinje","Bar","Herceg Novi","Berane",
                "Budva","Ulcinj","Tivat","Rozaje","Kotor","Danilovgrad","Mojkovac","Plav",
            }, SlvPre, None, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("MKD", Nationality.MKD, "북마케도니아", 1, 0.50f, "유럽", new[]
            {
                "Skopje","Bitola","Kumanovo","Prilep","Tetovo","Veles","Ohrid","Gostivar",
                "Strumica","Kavadarci","Stip","Kocani","Kicevo","Struga","Radovis","Gevgelija",
            }, SlvPre, None, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            N("NIR", Nationality.NIR, "북아일랜드", 1, 0.48f, "유럽", new[]
            {
                "Belfast","Derry","Lisburn","Newry","Bangor","Ballymena","Coleraine","Portadown",
                "Larne","Omagh","Armagh","Carrickfergus","Dungannon","Enniskillen","Newtownards","Antrim",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            N("FRO", Nationality.FRO, "페로 제도", 1, 0.40f, "유럽", new[]
            {
                "Torshavn","Klaksvik","Runavik","Tvoroyri","Fuglafjordur","Vagur","Sorvagur","Saltangara",
                "Hoyvik","Argir","Eidi","Sandur","Vestmanna","Leirvik","Skopun","Midvagur",
            }, NorPre, None, StGen, NamePools.NordicFirst, NamePools.NordicLast),

            N("KOS", Nationality.KOS, "코소보", 1, 0.46f, "유럽", new[]
            {
                "Pristina","Prizren","Peja","Gjakova","Gjilan","Mitrovica","Ferizaj","Vushtrri",
                "Podujeva","Suhareka","Rahovec","Drenas","Lipjan","Malisheva","Kamenica","Istog",
            }, NonePre, AsiaSuf, StGen, NamePools.BalkanFirst, NamePools.BalkanLast),

            // ── 아프리카 ──────────────────────────────────────────────
            N("BFA", Nationality.BFA, "부르키나파소", 1, 0.50f, "아프리카", new[]
            {
                "Ouagadougou","Bobo-Dioulasso","Koudougou","Banfora","Ouahigouya","Kaya","Tenkodogo","Fada",
                "Dori","Gaoua","Dedougou","Ziniare","Manga","Reo","Nouna","Diapaga",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("GUI", Nationality.GUI, "기니", 1, 0.48f, "아프리카", new[]
            {
                "Conakry","Nzerekore","Kankan","Kindia","Labe","Mamou","Boke","Faranah",
                "Siguiri","Gueckedou","Kissidougou","Macenta","Dalaba","Pita","Telimele","Fria",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("SDN", Nationality.SDN, "수단", 1, 0.48f, "아프리카", new[]
            {
                "Khartoum","Omdurman","Port Sudan","Kassala","Nyala","El Obeid","Wad Madani","Atbara",
                "El Fasher","Gedaref","Kosti","Sennar","Dongola","Rabak","Geneina","Damazin",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("LBY", Nationality.LBY, "리비아", 1, 0.48f, "아프리카", new[]
            {
                "Tripoli","Benghazi","Misrata","Zawiya","Bayda","Tobruk","Sabha","Khoms",
                "Derna","Sirte","Ajdabiya","Zliten","Gharyan","Ubari","Murzuq","Yefren",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("TAN", Nationality.TAN, "탄자니아", 1, 0.48f, "아프리카", new[]
            {
                "Dar es Salaam","Mwanza","Arusha","Dodoma","Mbeya","Morogoro","Tanga","Zanzibar",
                "Kigoma","Moshi","Tabora","Iringa","Songea","Musoma","Shinyanga","Bukoba",
            }, NonePre, AsiaSuf, StGen, NamePools.EastAfricanFirst, NamePools.EastAfricanLast),

            N("UGA", Nationality.UGA, "우간다", 1, 0.48f, "아프리카", new[]
            {
                "Kampala","Gulu","Lira","Mbarara","Jinja","Mbale","Masaka","Entebbe",
                "Soroti","Arua","Fort Portal","Kasese","Hoima","Tororo","Iganga","Kabale",
            }, NonePre, AsiaSuf, StGen, NamePools.EastAfricanFirst, NamePools.EastAfricanLast),

            N("ETH", Nationality.ETH, "에티오피아", 1, 0.48f, "아프리카", new[]
            {
                "Addis Ababa","Dire Dawa","Mekelle","Gondar","Hawassa","Bahir Dar","Jimma","Dessie",
                "Adama","Harar","Shashamane","Arba Minch","Debre Markos","Nekemte","Jijiga","Sodo",
            }, NonePre, AsiaSuf, StGen, NamePools.EastAfricanFirst, NamePools.EastAfricanLast),

            N("ZIM", Nationality.ZIM, "짐바브웨", 1, 0.48f, "아프리카", new[]
            {
                "Harare","Bulawayo","Mutare","Gweru","Kwekwe","Kadoma","Masvingo","Chinhoyi",
                "Marondera","Hwange","Chegutu","Zvishavane","Bindura","Victoria Falls","Rusape","Gokwe",
            }, NonePre, AsiaSuf, StGen, NamePools.SouthAfricanFirst, NamePools.SouthAfricanLast),

            N("MOZ", Nationality.MOZ, "모잠비크", 1, 0.48f, "아프리카", new[]
            {
                "Maputo","Matola","Beira","Nampula","Chimoio","Quelimane","Tete","Pemba",
                "Lichinga","Xai-Xai","Inhambane","Gurue","Maxixe","Angoche","Cuamba","Dondo",
            }, PorPre, None, StGen, NamePools.PortugueseFirst, NamePools.PortugueseLast),

            N("GAB", Nationality.GAB, "가봉", 1, 0.46f, "아프리카", new[]
            {
                "Libreville","Port-Gentil","Franceville","Oyem","Moanda","Mouila","Lambarene","Tchibanga",
                "Koulamoutou","Makokou","Bitam","Gamba","Ndjole","Booue","Mitzic","Okondja",
            }, FraPre, None, StFra, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("BEN", Nationality.BEN, "베냉", 1, 0.44f, "아프리카", new[]
            {
                "Cotonou","Porto-Novo","Parakou","Djougou","Bohicon","Kandi","Abomey","Natitingou",
                "Lokossa","Ouidah","Save","Nikki","Malanville","Pobe","Come","Dassa",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("TOG", Nationality.TOG, "토고", 1, 0.44f, "아프리카", new[]
            {
                "Lome","Sokode","Kara","Kpalime","Atakpame","Dapaong","Tsevie","Aneho",
                "Bassar","Tchamba","Niamtougou","Notse","Vogan","Badou","Sotouboua","Mango",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("MAD", Nationality.MAD, "마다가스카르", 1, 0.44f, "아프리카", new[]
            {
                "Antananarivo","Toamasina","Antsirabe","Fianarantsoa","Mahajanga","Toliara","Antsiranana","Ambovombe",
                "Sambava","Morondava","Manakara","Farafangana","Ihosy","Maevatanana","Moramanga","Tolagnaro",
            }, NonePre, AsiaSuf, StGen, NamePools.EastAfricanFirst, NamePools.EastAfricanLast),

            N("RWA", Nationality.RWA, "르완다", 1, 0.44f, "아프리카", new[]
            {
                "Kigali","Butare","Gitarama","Ruhengeri","Gisenyi","Byumba","Cyangugu","Kibuye",
                "Kibungo","Nyanza","Rwamagana","Musanze","Huye","Muhanga","Nyagatare","Karongi",
            }, NonePre, AsiaSuf, StGen, NamePools.EastAfricanFirst, NamePools.EastAfricanLast),

            N("CPV", Nationality.CPV, "카보베르데", 1, 0.42f, "아프리카", new[]
            {
                "Praia","Mindelo","Santa Maria","Assomada","Pedra Badejo","Sao Filipe","Tarrafal","Espargos",
                "Porto Novo","Ribeira Grande","Sal Rei","Vila Nova","Calheta","Mosteiros","Maio","Paul",
            }, PorPre, None, StGen, NamePools.PortugueseFirst, NamePools.PortugueseLast),

            // ── 아시아 ────────────────────────────────────────────────
            N("PHI", Nationality.PHI, "필리핀", 1, 0.48f, "아시아", new[]
            {
                "Manila","Cebu","Davao","Quezon City","Iloilo","Bacolod","Cagayan de Oro","Zamboanga",
                "Baguio","General Santos","Tacloban","Dumaguete","Angeles","Laoag","Butuan","Naga",
            }, NonePre, AsiaSuf, StGen, NamePools.FilipinoFirst, NamePools.FilipinoLast),

            N("SYR", Nationality.SYR, "시리아", 1, 0.50f, "아시아", new[]
            {
                "Damascus","Aleppo","Homs","Latakia","Hama","Deir ez-Zor","Raqqa","Tartus",
                "Idlib","Daraa","Hasakah","Qamishli","Suwayda","Palmyra","Jableh","Afrin",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("LIB", Nationality.LIB, "레바논", 1, 0.48f, "아시아", new[]
            {
                "Beirut","Tripoli","Sidon","Tyre","Zahle","Jounieh","Baalbek","Byblos",
                "Nabatieh","Aley","Batroun","Zgharta","Bcharre","Chtaura","Jezzine","Hermel",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("KUW", Nationality.KUW, "쿠웨이트", 1, 0.50f, "아시아", new[]
            {
                "Kuwait City","Hawalli","Salmiya","Jahra","Farwaniya","Fahaheel","Mangaf","Ahmadi",
                "Mahboula","Sabah Al Salem","Jabriya","Rumaithiya","Khaitan","Sulaibiya","Wafra","Abdali",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("BHR", Nationality.BHR, "바레인", 1, 0.48f, "아시아", new[]
            {
                "Manama","Muharraq","Riffa","Hamad Town","Isa Town","Sitra","Budaiya","Jidhafs",
                "Al Hidd","Zallaq","Sanad","Tubli","Galali","Arad","Malkiya","Durrat",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("OMA", Nationality.OMA, "오만", 1, 0.48f, "아시아", new[]
            {
                "Muscat","Salalah","Sohar","Nizwa","Sur","Ibri","Rustaq","Buraimi",
                "Seeb","Barka","Bahla","Khasab","Ibra","Samail","Adam","Duqm",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("HKG", Nationality.HKG, "홍콩", 1, 0.46f, "아시아", new[]
            {
                "Kowloon","Shatin","Tsuen Wan","Tuen Mun","Yuen Long","Tai Po","Kwun Tong","Sham Shui Po",
                "Wan Chai","Causeway Bay","Aberdeen","Stanley","Sai Kung","Tung Chung","Ma On Shan","North Point",
            }, NonePre, AsiaSuf, StGen, NamePools.ChineseFirst, NamePools.ChineseLast),

            N("TPE", Nationality.TPE, "대만", 1, 0.44f, "아시아", new[]
            {
                "Taipei","Kaohsiung","Taichung","Tainan","Hsinchu","Keelung","Chiayi","Taoyuan",
                "Changhua","Pingtung","Yilan","Hualien","Taitung","Miaoli","Nantou","Yunlin",
            }, NonePre, AsiaSuf, StGen, NamePools.ChineseFirst, NamePools.ChineseLast),

            N("MYA", Nationality.MYA, "미얀마", 1, 0.46f, "아시아", new[]
            {
                "Yangon","Mandalay","Naypyidaw","Bago","Mawlamyine","Taunggyi","Monywa","Sittwe",
                "Myitkyina","Pathein","Magway","Dawei","Pyay","Meiktila","Lashio","Hpa-An",
            }, NonePre, AsiaSuf, StGen, NamePools.BurmeseFirst, NamePools.BurmeseLast),

            N("CAM", Nationality.CAM, "캄보디아", 1, 0.44f, "아시아", new[]
            {
                "Phnom Penh","Siem Reap","Battambang","Sihanoukville","Kampong Cham","Poipet","Kampot","Pursat",
                "Takeo","Svay Rieng","Kratie","Prey Veng","Banteay Meanchey","Kampong Thom","Koh Kong","Stung Treng",
            }, NonePre, AsiaSuf, StGen, NamePools.KhmerFirst, NamePools.KhmerLast),

            N("TJK", Nationality.TJK, "타지키스탄", 1, 0.46f, "아시아", new[]
            {
                "Dushanbe","Khujand","Kulob","Bokhtar","Istaravshan","Tursunzoda","Panjakent","Konibodom",
                "Isfara","Vahdat","Khorugh","Hisor","Norak","Yovon","Danghara","Rasht",
            }, NonePre, AsiaSuf, StGen, NamePools.CentralAsianFirst, NamePools.CentralAsianLast),

            N("KGZ", Nationality.KGZ, "키르기스스탄", 1, 0.46f, "아시아", new[]
            {
                "Bishkek","Osh","Jalal-Abad","Karakol","Tokmok","Naryn","Talas","Batken",
                "Kant","Balykchy","Kyzyl-Kiya","Cholpon-Ata","Uzgen","Isfana","Kara-Balta","Sulyukta",
            }, NonePre, AsiaSuf, StGen, NamePools.CentralAsianFirst, NamePools.CentralAsianLast),
            // ── 2026-11-03 (4차) 유럽 초소국 ─────────────────────────
            N("AND", Nationality.AND, "안도라", 1, 0.38f, "유럽", new[]
            {
                "Andorra la Vella","Escaldes","Encamp","Sant Julia","La Massana","Ordino","Canillo","Pas de la Casa",
                "Arinsal","Soldeu","Santa Coloma","Anyos","Erts","Llorts","Sispony","El Tarter",
            }, EspPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("SMR", Nationality.SMR, "산마리노", 1, 0.36f, "유럽", new[]
            {
                "Serravalle","Borgo Maggiore","San Marino","Domagnano","Fiorentino","Acquaviva","Faetano","Chiesanuova",
                "Montegiardino","Dogana","Murata","Cailungo","Falciano","Gualdicciola","Montalbo","Torraccia",
            }, ItaPre, None, StIta, NamePools.ItalianFirst, NamePools.ItalianLast),

            N("GIB", Nationality.GIB, "지브롤터", 1, 0.38f, "유럽", new[]
            {
                "Gibraltar","Europa Point","Catalan Bay","Westside","Rosia","Waterport","Sandpits","Glacis",
                "Devils Tower","Varyl Begg","Laguna","Montagu","Bayside","Alameda","Chatham","Lathbury",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            N("LIE", Nationality.LIE, "리히텐슈타인", 1, 0.36f, "유럽", new[]
            {
                "Vaduz","Schaan","Triesen","Balzers","Eschen","Mauren","Triesenberg","Ruggell",
                "Gamprin","Schellenberg","Planken","Nendeln","Bendern","Malbun","Steg","Masescha",
            }, GerPre, None, StGer, NamePools.GermanFirst, NamePools.GermanLast),

            // ── 북중미 ────────────────────────────────────────────────
            N("CUB", Nationality.CUB, "쿠바", 1, 0.46f, "북중미", new[]
            {
                "Havana","Santiago de Cuba","Camaguey","Holguin","Santa Clara","Guantanamo","Bayamo","Cienfuegos",
                "Pinar del Rio","Las Tunas","Matanzas","Ciego de Avila","Sancti Spiritus","Manzanillo","Cardenas","Moron",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("DOM", Nationality.DOM, "도미니카공화국", 1, 0.44f, "북중미", new[]
            {
                "Santo Domingo","Santiago","La Romana","San Pedro","San Cristobal","Puerto Plata","Higuey","Bani",
                "Moca","Bonao","Barahona","Azua","Mao","Nagua","Cotui","Jarabacoa",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("HAI", Nationality.HAI, "아이티", 1, 0.46f, "북중미", new[]
            {
                "Port-au-Prince","Cap-Haitien","Gonaives","Les Cayes","Jacmel","Jeremie","Hinche","Port-de-Paix",
                "Saint-Marc","Petion-Ville","Leogane","Miragoane","Fort-Liberte","Mirebalais","Croix-des-Bouquets","Limbe",
            }, FraPre, None, StFra, NamePools.FrenchFirst, NamePools.FrenchLast),

            N("TRI", Nationality.TRI, "트리니다드토바고", 1, 0.44f, "북중미", new[]
            {
                "Port of Spain","San Fernando","Chaguanas","Arima","Point Fortin","Scarborough","Couva","Sangre Grande",
                "Princes Town","Tunapuna","Diego Martin","Siparia","Rio Claro","Penal","Mayaro","Toco",
            }, EngPre, EngSuf, StEng, NamePools.AngloFirst, NamePools.AngloLast),

            N("NCA", Nationality.NCA, "니카라과", 1, 0.42f, "북중미", new[]
            {
                "Managua","Leon","Granada","Masaya","Chinandega","Matagalpa","Esteli","Jinotega",
                "Bluefields","Rivas","Boaco","Juigalpa","Ocotal","Somoto","Jinotepe","Diriamba",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("SLV", Nationality.SLV, "엘살바도르", 1, 0.46f, "북중미", new[]
            {
                "San Salvador","Santa Ana","San Miguel","Soyapango","Mejicanos","Santa Tecla","Apopa","Delgado",
                "Sonsonate","Usulutan","Ahuachapan","Zacatecoluca","Cojutepeque","Chalchuapa","Metapan","La Union",
            }, ArgPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("SUR", Nationality.SUR, "수리남", 1, 0.40f, "북중미", new[]
            {
                "Paramaribo","Lelydorp","Nieuw Nickerie","Moengo","Nieuw Amsterdam","Albina","Groningen","Brownsweg",
                "Onverwacht","Wageningen","Totness","Brokopondo","Marienburg","Meerzorg","Domburg","Santo Boma",
            }, NedPre, None, StGen, NamePools.DutchFirst, NamePools.DutchLast),

            N("CUW", Nationality.CUW, "퀴라소", 1, 0.40f, "북중미", new[]
            {
                "Willemstad","Barber","Soto","Westpunt","Sint Michiel","Dorp Sint Willibrordus","Tera Kora","Montana",
                "Jan Thiel","Punda","Otrobanda","Scharloo","Brievengat","Rio Canario","Koraal Specht","Julianadorp",
            }, NedPre, None, StGen, NamePools.DutchFirst, NamePools.DutchLast),

            // ── 아시아 ────────────────────────────────────────────────
            N("PRK", Nationality.PRK, "북한", 1, 0.48f, "아시아", new[]
            {
                "Pyongyang","Hamhung","Chongjin","Nampo","Wonsan","Sinuiju","Tanchon","Kaechon",
                "Kaesong","Sariwon","Haeju","Hyesan","Kanggye","Anju","Tokchon","Rason",
            }, NonePre, AsiaSuf, StGen, NamePools.KoreanFirst, NamePools.KoreanLast),

            N("MNG", Nationality.MNG, "몽골", 1, 0.40f, "아시아", new[]
            {
                "Ulaanbaatar","Erdenet","Darkhan","Choibalsan","Murun","Khovd","Olgii","Bayankhongor",
                "Arvaikheer","Ulaangom","Sainshand","Altai","Dalanzadgad","Zuunmod","Baruun-Urt","Tsetserleg",
            }, NonePre, AsiaSuf, StGen, NamePools.CentralAsianFirst, NamePools.CentralAsianLast),

            N("NEP", Nationality.NEP, "네팔", 1, 0.40f, "아시아", new[]
            {
                "Kathmandu","Pokhara","Lalitpur","Biratnagar","Bharatpur","Birgunj","Dharan","Butwal",
                "Hetauda","Nepalgunj","Janakpur","Dhangadhi","Itahari","Bhaktapur","Damak","Tulsipur",
            }, NonePre, AsiaSuf, StGen, NamePools.IndianFirst, NamePools.IndianLast),

            N("BAN", Nationality.BAN, "방글라데시", 1, 0.42f, "아시아", new[]
            {
                "Dhaka","Chittagong","Khulna","Rajshahi","Sylhet","Barisal","Rangpur","Comilla",
                "Mymensingh","Narayanganj","Gazipur","Jessore","Bogra","Dinajpur","Tangail","Cox Bazar",
            }, NonePre, AsiaSuf, StGen, NamePools.IndianFirst, NamePools.IndianLast),

            N("SRI", Nationality.SRI, "스리랑카", 1, 0.38f, "아시아", new[]
            {
                "Colombo","Kandy","Galle","Jaffna","Negombo","Trincomalee","Batticaloa","Matara",
                "Anuradhapura","Ratnapura","Badulla","Kurunegala","Puttalam","Hambantota","Vavuniya","Gampaha",
            }, NonePre, AsiaSuf, StGen, NamePools.IndianFirst, NamePools.IndianLast),

            N("MDV", Nationality.MDV, "몰디브", 1, 0.38f, "아시아", new[]
            {
                "Male","Addu","Fuvahmulah","Kulhudhuffushi","Thinadhoo","Naifaru","Hinnavaru","Eydhafushi",
                "Dhidhdhoo","Mahibadhoo","Veymandoo","Funadhoo","Manadhoo","Ungoofaaru","Gan","Hulhumale",
            }, NonePre, AsiaSuf, StGen, NamePools.IndianFirst, NamePools.IndianLast),

            N("PAK", Nationality.PAK, "파키스탄", 1, 0.42f, "아시아", new[]
            {
                "Karachi","Lahore","Faisalabad","Rawalpindi","Multan","Peshawar","Quetta","Islamabad",
                "Sialkot","Gujranwala","Hyderabad","Sargodha","Bahawalpur","Sukkur","Mardan","Abbottabad",
            }, NonePre, AsiaSuf, StGen, NamePools.IndianFirst, NamePools.IndianLast),

            N("AFG", Nationality.AFG, "아프가니스탄", 1, 0.40f, "아시아", new[]
            {
                "Kabul","Kandahar","Herat","Mazar-i-Sharif","Jalalabad","Kunduz","Ghazni","Lashkargah",
                "Taloqan","Pul-e-Khumri","Khost","Bamyan","Farah","Gardez","Sheberghan","Maymana",
            }, NonePre, AsiaSuf, StGen, NamePools.PersianFirst, NamePools.PersianLast),

            N("TKM", Nationality.TKM, "투르크메니스탄", 1, 0.42f, "아시아", new[]
            {
                "Ashgabat","Turkmenabat","Dasoguz","Mary","Balkanabat","Bayramaly","Turkmenbashi","Tejen",
                "Serdar","Kaka","Gazojak","Koneurgench","Atamyrat","Yoloten","Murgap","Sarahs",
            }, NonePre, AsiaSuf, StGen, NamePools.CentralAsianFirst, NamePools.CentralAsianLast),

            N("YEM", Nationality.YEM, "예멘", 1, 0.40f, "아시아", new[]
            {
                "Sanaa","Aden","Taiz","Hodeidah","Ibb","Mukalla","Dhamar","Amran",
                "Sayun","Zinjibar","Hajjah","Saada","Marib","Lahij","Bajil","Zabid",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("PLE", Nationality.PLE, "팔레스타인", 1, 0.42f, "아시아", new[]
            {
                "Gaza","Hebron","Nablus","Ramallah","Jenin","Bethlehem","Tulkarm","Qalqilya",
                "Khan Yunis","Rafah","Jericho","Salfit","Tubas","Deir al-Balah","Beit Lahia","Azzun",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("LAO", Nationality.LAO, "라오스", 1, 0.38f, "아시아", new[]
            {
                "Vientiane","Savannakhet","Pakse","Luang Prabang","Thakhek","Xam Neua","Phonsavan","Attapeu",
                "Muang Xay","Salavan","Sayaboury","Vang Vieng","Paksan","Luang Namtha","Champasak","Phongsaly",
            }, NonePre, AsiaSuf, StGen, NamePools.ThaiFirst, NamePools.ThaiLast),

            // ── 아프리카 ──────────────────────────────────────────────
            N("CGO", Nationality.CGO, "콩고", 1, 0.44f, "아프리카", new[]
            {
                "Brazzaville","Pointe-Noire","Dolisie","Nkayi","Ouesso","Impfondo","Madingou","Owando",
                "Sibiti","Gamboma","Djambala","Kinkala","Mossendjo","Loandjili","Ewo","Kelle",
            }, FraPre, None, StFra, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("NIG", Nationality.NIG, "니제르", 1, 0.40f, "아프리카", new[]
            {
                "Niamey","Zinder","Maradi","Agadez","Tahoua","Dosso","Arlit","Tessaoua",
                "Birnin Konni","Gaya","Diffa","Tillaberi","Madaoua","Magaria","Ayorou","Dogondoutchi",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("MTN", Nationality.MTN, "모리타니", 1, 0.40f, "아프리카", new[]
            {
                "Nouakchott","Nouadhibou","Rosso","Kaedi","Zouerate","Kiffa","Atar","Selibaby",
                "Aleg","Boutilimit","Tidjikja","Akjoujt","Nema","Aioun","Timbedra","Chinguetti",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("BOT", Nationality.BOT, "보츠와나", 1, 0.42f, "아프리카", new[]
            {
                "Gaborone","Francistown","Molepolole","Maun","Serowe","Selebi-Phikwe","Kanye","Mochudi",
                "Mahalapye","Palapye","Lobatse","Ramotswa","Tlokweng","Jwaneng","Letlhakane","Ghanzi",
            }, NonePre, AsiaSuf, StGen, NamePools.SouthAfricanFirst, NamePools.SouthAfricanLast),

            N("NAM", Nationality.NAM, "나미비아", 1, 0.42f, "아프리카", new[]
            {
                "Windhoek","Walvis Bay","Swakopmund","Oshakati","Rundu","Katima Mulilo","Otjiwarongo","Gobabis",
                "Keetmanshoop","Tsumeb","Grootfontein","Rehoboth","Mariental","Okahandja","Ondangwa","Luderitz",
            }, NonePre, AsiaSuf, StGen, NamePools.SouthAfricanFirst, NamePools.SouthAfricanLast),

            N("MWI", Nationality.MWI, "말라위", 1, 0.42f, "아프리카", new[]
            {
                "Lilongwe","Blantyre","Mzuzu","Zomba","Kasungu","Mangochi","Karonga","Salima",
                "Nkhotakota","Liwonde","Balaka","Dedza","Mchinji","Nsanje","Rumphi","Mulanje",
            }, NonePre, AsiaSuf, StGen, NamePools.EastAfricanFirst, NamePools.EastAfricanLast),

            N("BDI", Nationality.BDI, "부룬디", 1, 0.40f, "아프리카", new[]
            {
                "Bujumbura","Gitega","Ngozi","Muyinga","Ruyigi","Kayanza","Rutana","Bururi",
                "Makamba","Cibitoke","Karuzi","Muramvya","Kirundo","Rumonge","Bubanza","Mwaro",
            }, FraPre, None, StFra, NamePools.EastAfricanFirst, NamePools.EastAfricanLast),

            N("EQG", Nationality.EQG, "적도기니", 1, 0.40f, "아프리카", new[]
            {
                "Malabo","Bata","Ebebiyin","Aconibe","Mongomo","Luba","Evinayong","Mbini",
                "Micomeseng","Anisok","Nsok","Rebola","Baney","Kogo","Niefang","Bitica",
            }, EspPre, None, StEsp, NamePools.SpanishFirst, NamePools.SpanishLast),

            N("SLE", Nationality.SLE, "시에라리온", 1, 0.40f, "아프리카", new[]
            {
                "Freetown","Bo","Kenema","Makeni","Koidu","Lunsar","Port Loko","Waterloo",
                "Kabala","Magburaka","Moyamba","Bonthe","Kailahun","Pujehun","Kambia","Segbwema",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("LBR", Nationality.LBR, "라이베리아", 1, 0.40f, "아프리카", new[]
            {
                "Monrovia","Gbarnga","Kakata","Buchanan","Ganta","Zwedru","Harper","Voinjama",
                "Robertsport","Greenville","Tubmanburg","Sanniquellie","Bensonville","Fish Town","Barclayville","Cestos",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("GAM", Nationality.GAM, "감비아", 1, 0.40f, "아프리카", new[]
            {
                "Banjul","Serekunda","Brikama","Bakau","Farafenni","Lamin","Basse","Gunjur",
                "Soma","Kerewan","Mansa Konko","Janjanbureh","Kuntaur","Sukuta","Barra","Essau",
            }, NonePre, AsiaSuf, StGen, NamePools.WestAfricanFirst, NamePools.WestAfricanLast),

            N("GNB", Nationality.GNB, "기니비사우", 1, 0.38f, "아프리카", new[]
            {
                "Bissau","Bafata","Gabu","Canchungo","Mansoa","Buba","Catio","Farim",
                "Quinhamel","Bolama","Bubaque","Cacheu","Fulacunda","Sao Domingos","Pirada","Quebo",
            }, PorPre, None, StGen, NamePools.PortugueseFirst, NamePools.PortugueseLast),

            N("CHA", Nationality.CHA, "차드", 1, 0.38f, "아프리카", new[]
            {
                "NDjamena","Moundou","Sarh","Abeche","Kelo","Koumra","Pala","Am Timan",
                "Bongor","Mongo","Doba","Ati","Faya","Oum Hadjer","Bitkine","Mao",
            }, FraPre, None, StFra, NamePools.ArabicFirst, NamePools.ArabicLast),

            N("SOM", Nationality.SOM, "소말리아", 1, 0.38f, "아프리카", new[]
            {
                "Mogadishu","Hargeisa","Bosaso","Kismayo","Galkayo","Berbera","Baidoa","Burao",
                "Jowhar","Beledweyne","Garowe","Marka","Afgooye","Erigavo","Las Anod","Qardho",
            }, NonePre, AsiaSuf, StGen, NamePools.ArabicFirst, NamePools.ArabicLast),

            // ── 오세아니아 ────────────────────────────────────────────
            N("SOL", Nationality.SOL, "솔로몬 제도", 1, 0.40f, "오세아니아", new[]
            {
                "Honiara","Gizo","Auki","Munda","Tulagi","Buala","Kirakira","Lata",
                "Taro","Tigoa","Noro","Malango","Avuavu","Yandina","Seghe","Ringgi",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),

            N("VAN", Nationality.VAN, "바누아투", 1, 0.38f, "오세아니아", new[]
            {
                "Port Vila","Luganville","Norsup","Isangel","Saratamata","Lakatoro","Sola","Lenakel",
                "Longana","Port Olry","Melsisi","Craig Cove","Lamap","Ipota","Vila North","Mele",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),

            N("NCL", Nationality.NCL, "뉴칼레도니아", 1, 0.40f, "오세아니아", new[]
            {
                "Noumea","Mont-Dore","Dumbea","Paita","We","Kone","Bourail","Poindimie",
                "La Foa","Canala","Thio","Hienghene","Koumac","Lifou","Mare","Houailou",
            }, FraPre, None, StFra, NamePools.FrenchFirst, NamePools.FrenchLast),

            N("TAH", Nationality.TAH, "타히티", 1, 0.38f, "오세아니아", new[]
            {
                "Papeete","Faaa","Punaauia","Pirae","Mahina","Paea","Arue","Papara",
                "Taiarapu","Moorea","Teva I Uta","Hitiaa","Taravao","Papenoo","Afaahiti","Tiarei",
            }, FraPre, None, StFra, NamePools.FrenchFirst, NamePools.FrenchLast),

            N("SAM", Nationality.SAM, "사모아", 1, 0.34f, "오세아니아", new[]
            {
                "Apia","Vaitele","Faleula","Siusega","Vailele","Leulumoega","Mulifanua","Salelologa",
                "Safotu","Asau","Lotofaga","Falealupo","Satupaitea","Lalomanu","Manono","Sataua",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),

            N("TGA", Nationality.TGA, "통가", 1, 0.34f, "오세아니아", new[]
            {
                "Nukualofa","Neiafu","Haveluloto","Vaini","Pangai","Ohonua","Hihifo","Mua",
                "Kolonga","Lapaha","Houma","Fuaamotu","Talihau","Nomuka","Niutoua","Kolovai",
            }, NonePre, AsiaSuf, StGen, NamePools.AngloFirst, NamePools.AngloLast),
        };



    }
}
