namespace FootballTactician.Data
{
    /// <summary>
    /// 이름을 붙여 쓰는 방식. <b>나라마다 다르다.</b>
    ///
    /// 서양 이름은 "해리 워커"처럼 이름을 앞에 두고 띄어 쓰지만, 한국과 중국은 성에
    /// 이름을 붙여 "김민준"·"왕웨이"로 쓰고, 일본은 성과 이름을 띄워 "다나카 하루토"로
    /// 쓴다. 한 가지 방식으로 다 찍으면 "민준 김" 같은 말이 나온다.
    ///
    /// 저장에는 이름과 성이 따로 들어간다. 붙이는 방식만 여기서 정하므로
    /// 세이브 포맷은 건드리지 않는다.
    /// </summary>
    public static class NameStyle
    {
        enum Order
        {
            GivenFamily,        // 해리 워커
            FamilyGivenTight,   // 김민준
            FamilyGivenSpaced,  // 다나카 하루토
        }

        /// <summary>
        /// 한글이 아닌 이름(예전 세이브의 로마자 표기)은 서양식으로 띄어 쓴다.
        ///
        /// 이것이 없으면 예전 세이브의 "Jin-sung Choi" 가 "ChoiJin-sung" 으로 붙어 버린다 —
        /// 성을 앞에 붙이는 규칙은 한글 표기에서만 뜻이 있다.
        /// </summary>
        static bool Hangul(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (c >= '가' && c <= '힣') return true;
            return false;
        }

        static Order Of(Nationality nat) => nat switch
        {
            Nationality.KOR => Order.FamilyGivenTight,
            Nationality.CHN => Order.FamilyGivenTight,
            Nationality.JPN => Order.FamilyGivenSpaced,
            _ => Order.GivenFamily,
        };

        /// <summary>화면에 쓰는 온전한 이름.</summary>
        public static string Full(Nationality nat, string first, string last)
        {
            if (!Hangul(first) && !Hangul(last)) return $"{first} {last}";
            return Join(Of(nat), first, last);
        }

        static string Join(Order order, string first, string last) => order switch
        {
            Order.FamilyGivenTight => $"{last}{first}",
            Order.FamilyGivenSpaced => $"{last} {first}",
            _ => $"{first} {last}",
        };

        /// <summary>
        /// 좁은 칸에 쓰는 짧은 이름. 서양은 성만("워커"), 성을 앞에 쓰는 나라는
        /// 온전한 이름을 그대로 쓴다 — "김"만 남기면 누구인지 알 수 없다.
        /// </summary>
        public static string Short(Nationality nat, string first, string last)
            => Of(nat) == Order.GivenFamily || (!Hangul(first) && !Hangul(last))
                ? last
                : Full(nat, first, last);
    }
}
