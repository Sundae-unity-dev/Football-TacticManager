namespace FootballTactician.World
{
    /// <summary>
    /// 지역별 사람 이름 풀.
    ///
    /// 나라마다 이름을 따로 쓰면 마흔 나라에 이름 이천 개가 필요하다. 이름은 나라가
    /// 아니라 <b>언어권</b>을 따르므로 — 아르헨티나 선수와 멕시코 선수는 같은 결의
    /// 이름을 쓴다 — 언어권으로 묶어 돌려 쓴다. 도시만 나라마다 다르면 충분하다.
    ///
    /// 실존 선수의 이름을 그대로 쓰지 않는다. 흔한 이름과 흔한 성을 섞어 만들 뿐이다.
    /// </summary>
    static class NamePools
    {
        // ── 유럽 ──────────────────────────────────────────────────────

        public static readonly string[] AngloFirst =
        {
            "Harry","Jack","Oliver","Charlie","Thomas","George","James","Will","Alfie","Daniel",
            "Lewis","Callum","Owen","Reece","Dean","Marcus","Kyle","Liam","Connor","Ryan",
            "Nathan","Joel","Toby","Elliot",
        };
        public static readonly string[] AngloLast =
        {
            "Walker","Hughes","Bennett","Carter","Shaw","Reid","Holt","Grant","Pearce","Lowe",
            "Mason","Doyle","Burke","Fletcher","Sutton","Barnes","Nash","Rowe","Dalton","Keane",
            "Wilkins","Harper","Boyd","Finch",
        };

        public static readonly string[] SpanishFirst =
        {
            "Alvaro","Javier","Sergio","Pablo","Iker","Raul","Diego","Marcos","Adrian","Hugo",
            "Nicolas","Mateo","Tomas","Bruno","Ivan","Lucas","Emilio","Andres","Gonzalo","Rodrigo",
            "Facundo","Ezequiel","Santiago","Joaquin",
        };
        public static readonly string[] SpanishLast =
        {
            "Navas","Quesada","Sabino","Carvajal","Juarez","Maldonado","Salgado","Urbina","Zarella","Pinto",
            "Arrieta","Bermudez","Cortes","Delgado","Esparza","Figueroa","Galvan","Herrera","Ibarra","Jimenez",
            "Lozano","Medina","Ocampo","Peralta",
        };

        public static readonly string[] PortugueseFirst =
        {
            "Joao","Rui","Tiago","Bruno","Diogo","Nuno","Ricardo","Andre","Miguel","Pedro",
            "Rafael","Vitor","Fabio","Gonzalo","Hugo","Leonardo","Matheus","Caio","Thiago","Vinicius",
            "Everton","Gustavo","Murilo","Anderson",
        };
        public static readonly string[] PortugueseLast =
        {
            "Almeida","Barbosa","Carvalho","Dias","Esteves","Ferreira","Gomes","Henriques","Inacio","Jardim",
            "Lima","Macedo","Neves","Oliveira","Pacheco","Queiroz","Ramos","Soares","Teixeira","Vieira",
            "Azevedo","Bastos","Cardoso","Moreira",
        };

        public static readonly string[] ItalianFirst =
        {
            "Marco","Luca","Andrea","Matteo","Davide","Simone","Alessio","Federico","Lorenzo","Giulio",
            "Riccardo","Stefano","Nicolo","Emanuele","Fabio","Gianluca","Pietro","Tommaso","Vito","Enrico",
            "Dario","Cristian","Samuele","Mirko",
        };
        public static readonly string[] ItalianLast =
        {
            "Bellandi","Cestari","Donatelli","Fioravanti","Iannone","Novelli","Oriani","Scalise","Tagliabue","Zambrano",
            "Albertini","Bonetti","Calvani","Durante","Ferraro","Gallo","Lombardi","Marchetti","Nicolosi","Perazzi",
            "Rinaldi","Sartori","Trevisan","Vergani",
        };

        public static readonly string[] GermanFirst =
        {
            "Lukas","Jonas","Finn","Niklas","Tobias","Maximilian","Felix","Moritz","Jannik","Leon",
            "Benedikt","Quirin","Kilian","Markus","Sebastian","Florian","Dominik","Matthias","Philipp","Simon",
            "Erik","Hendrik","Till","Lennart",
        };
        public static readonly string[] GermanLast =
        {
            "Achterberg","Brennecke","Eberhart","Gerstner","Haberland","Illertal","Reinhardt","Schattner","Treuberg","Uhlmann",
            "Vogler","Wendler","Ziegenhain","Brandt","Dreyer","Engel","Fichtner","Grabner","Hartmann","Kessler",
            "Lindner","Mertens","Ostermann","Pretzel",
        };

        public static readonly string[] FrenchFirst =
        {
            "Hugo","Theo","Enzo","Nathan","Maxime","Lucas","Antoine","Clement","Baptiste","Quentin",
            "Romain","Valentin","Alexis","Mathis","Yanis","Corentin","Gaetan","Loic","Noe","Timothe",
            "Adrien","Florent","Jules","Remi",
        };
        public static readonly string[] FrenchLast =
        {
            "Beaufort","Chastain","Delacroix","Escande","Fauvel","Gautier","Hamelin","Jourdain","Lavigne","Marchand",
            "Noailles","Ollivier","Perrin","Quilliot","Rossignol","Sauvage","Thibault","Vacher","Arnaud","Boucher",
            "Chevalier","Dumas","Fournier","Granger",
        };

        public static readonly string[] DutchFirst =
        {
            "Sven","Bram","Daan","Jesse","Thijs","Ruben","Mats","Stijn","Jurgen","Koen",
            "Lars","Niels","Pim","Roel","Teun","Wouter","Joost","Marnix","Sander","Dirk",
            "Gijs","Hidde","Kars","Olivier",
        };
        public static readonly string[] DutchLast =
        {
            "Van Dijk","Bakker","De Vries","Jansen","Visser","Smit","Meijer","Bos","Vos","Peters",
            "Hendriks","Dekker","Willems","Van Leeuwen","Scholten","Kuipers","Brouwer","Timmermans","Bosman","Vermeer",
            "Huisman","Kramer","Nagel","Prins",
        };

        public static readonly string[] NordicFirst =
        {
            "Jonas","Mikal","Aren","Sven","Erik","Rune","Olav","Nils","Kasper","Teodor",
            "Halvard","Birger","Steinar","Vetle","Arne","Torvald","Leif","Gunnar","Eivind","Sigurd",
            "Trygve","Audun","Vidar","Harald",
        };
        public static readonly string[] NordicLast =
        {
            "Aalberg","Bjornstad","Dahlstrom","Falk","Gulbrandsen","Halden","Jostad","Kvist","Markusson","Norrby",
            "Osk","Rodland","Sundby","Tangen","Vangen","Wold","Berg","Lind","Haugen","Melby",
            "Fjelstad","Rognstad","Viken","Eide",
        };

        public static readonly string[] SlavicFirst =
        {
            "Pavel","Marek","Tomas","Jakub","Ondrej","Lukasz","Mateusz","Bartosz","Kamil","Dawid",
            "Andrei","Dmitri","Sergei","Vadim","Oleg","Yuri","Ivan","Roman","Viktor","Stanislav",
            "Milan","Radek","Zdenek","Igor",
        };
        public static readonly string[] SlavicLast =
        {
            "Novak","Kovacs","Zielinski","Wojcik","Kaminski","Lewandowicz","Mazur","Sokolov","Volkov","Antonov",
            "Bondar","Chernov","Danylo","Fedorov","Hrytsenko","Kozlov","Marchuk","Nazarenko","Petrov","Romanenko",
            "Shevchuk","Tkachenko","Vasilenko","Yurchenko",
        };

        public static readonly string[] BalkanFirst =
        {
            "Nikola","Stefan","Marko","Luka","Dusan","Filip","Vladan","Aleksa","Bojan","Dejan",
            "Giorgos","Dimitris","Kostas","Nikos","Panos","Stelios","Vasilis","Yiannis","Thanos","Petros",
            "Andrej","Igor","Mladen","Zoran",
        };
        public static readonly string[] BalkanLast =
        {
            "Jovanovic","Petrovic","Nikolic","Stankovic","Milosevic","Vukovic","Radic","Babic","Markovic","Ilic",
            "Papadakis","Georgiou","Nikolaou","Vasiliou","Christou","Antoniou","Dimitriou","Stavrou","Pappas","Rizos",
            "Horvat","Kovacic","Tomic","Vidic",
        };

        public static readonly string[] TurkishFirst =
        {
            "Emre","Burak","Cenk","Hakan","Kerem","Mert","Ozan","Serdar","Tolga","Umut",
            "Yusuf","Baris","Deniz","Furkan","Gokhan","Ilhan","Kaan","Levent","Murat","Onur",
            "Rifat","Selim","Taner","Volkan",
        };
        public static readonly string[] TurkishLast =
        {
            "Aydin","Yilmaz","Demir","Kaya","Sahin","Celik","Arslan","Dogan","Kilic","Aslan",
            "Ozturk","Koc","Tekin","Erdem","Polat","Sari","Gunes","Bulut","Korkmaz","Turan",
            "Akgun","Balci","Cetin","Duran",
        };

        // ── 아시아 ────────────────────────────────────────────────────

        public static readonly string[] KoreanFirst =
        {
            "Min-jun","Seo-jun","Do-yun","Ji-hun","Hyun-woo","Jun-seo","Yu-jin","Tae-yang","Si-woo","Ha-jun",
            "Eun-woo","Geon-woo","Jae-min","Dong-hyun","Sang-woo","Jin-sung","Hyo-jun","Kyung-min","Woo-jin","Sung-hoon",
            "Chan-young","Nam-gil","Yeon-jae","Bo-ram",
        };
        public static readonly string[] KoreanLast =
        {
            "Kim","Lee","Park","Choi","Jung","Kang","Cho","Yoon","Jang","Lim",
            "Han","Oh","Seo","Shin","Kwon","Hwang","Ahn","Song","Hong","Ryu",
            "Jeon","Moon","Nam","Bae",
        };

        public static readonly string[] JapaneseFirst =
        {
            "Haruto","Sota","Yuto","Ren","Riku","Kaito","Hinata","Yuma","Sora","Takumi",
            "Daiki","Kenta","Shota","Naoki","Yusuke","Hiroto","Kazuki","Ryota","Tatsuya","Masaki",
            "Keisuke","Shinji","Takeshi","Yoshito",
        };
        public static readonly string[] JapaneseLast =
        {
            "Tanaka","Suzuki","Sato","Watanabe","Ito","Yamamoto","Nakamura","Kobayashi","Kato","Yoshida",
            "Yamada","Sasaki","Matsumoto","Inoue","Kimura","Hayashi","Shimizu","Saito","Mori","Ikeda",
            "Hashimoto","Ogawa","Ishii","Maeda",
        };

        public static readonly string[] ChineseFirst =
        {
            "Wei","Hao","Jun","Lei","Ming","Peng","Qiang","Tao","Xin","Yang",
            "Zhen","Bo","Chen","Dong","Feng","Gang","Hui","Jian","Kun","Long",
            "Nan","Qi","Sheng","Yu",
        };
        public static readonly string[] ChineseLast =
        {
            "Wang","Li","Zhang","Liu","Chen","Yang","Huang","Zhao","Wu","Zhou",
            "Xu","Sun","Ma","Zhu","Hu","Guo","He","Lin","Gao","Luo",
            "Zheng","Liang","Xie","Song",
        };

        public static readonly string[] ArabicFirst =
        {
            "Omar","Youssef","Karim","Tarek","Hassan","Ali","Mahmoud","Ahmed","Bilal","Khaled",
            "Nasser","Rami","Sami","Walid","Ziad","Faisal","Hamza","Ibrahim","Jamal","Mohsen",
            "Nabil","Rashid","Saleh","Yahya",
        };
        public static readonly string[] ArabicLast =
        {
            "Al-Amri","Al-Dosari","Al-Harbi","Al-Qahtani","Al-Shehri","Ben Ali","Benzarti","Cherif","Dahmani","El-Masry",
            "Fahmy","Ghanem","Haddad","Idrissi","Jaber","Kassem","Lahlou","Mansour","Najjar","Othman",
            "Qadir","Rahmani","Saleh","Zouari",
        };

        public static readonly string[] PersianFirst =
        {
            "Ali","Reza","Mehdi","Hossein","Saeed","Vahid","Milad","Omid","Farhad","Kaveh",
            "Payam","Ramin","Shahin","Arash","Behnam","Danial","Ehsan","Farzad","Hamed","Iman",
            "Kian","Nima","Pouya","Sina",
        };
        public static readonly string[] PersianLast =
        {
            "Ahmadi","Bagheri","Dehghan","Ebrahimi","Fallah","Ghorbani","Hashemi","Jafari","Karimi","Lotfi",
            "Mousavi","Nouri","Parsa","Rahimi","Sadeghi","Tehrani","Vafaei","Yazdani","Zarei","Amiri",
            "Beheshti","Delavar","Farahani","Khosravi",
        };

        // ── 아프리카 ──────────────────────────────────────────────────

        public static readonly string[] WestAfricanFirst =
        {
            "Kwame","Kofi","Yaw","Emeka","Chidi","Obi","Musa","Ibrahim","Sadio","Mamadou",
            "Ousmane","Abdoulaye","Cheikh","Lamine","Moussa","Seydou","Bakary","Daouda","Fode","Ismaila",
            "Kalidou","Oumar","Souleymane","Yacouba",
        };
        public static readonly string[] WestAfricanLast =
        {
            "Mensah","Owusu","Asante","Boateng","Okafor","Eze","Nwosu","Adeyemi","Diallo","Traore",
            "Keita","Cisse","Camara","Sow","Toure","Bamba","Konate","Sylla","Doumbia","Fofana",
            "Gueye","Ndiaye","Sarr","Thiam",
        };

        public static readonly string[] SouthAfricanFirst =
        {
            "Thabo","Sipho","Lerato","Mandla","Bongani","Katlego","Themba","Tshepo","Kagiso","Lungile",
            "Sibusiso","Zwelithini","Andile","Dumisani","Hlompho","Jabulani","Mfundo","Nkosi","Phumlani","Sandile",
            "Vusi","Xolani","Zakhele","Mpho",
        };
        public static readonly string[] SouthAfricanLast =
        {
            "Dlamini","Khumalo","Mokoena","Ndlovu","Nkosi","Zulu","Mabaso","Sithole","Mthembu","Radebe",
            "Mahlangu","Maluleka","Molefe","Ngcobo","Shabalala","Tshabalala","Van Wyk","Botha","Pretorius","Du Toit",
            "Jacobs","Daniels","Arendse","Williams",
        };
    }
}
