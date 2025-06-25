using System;
using System.Collections.Generic;

namespace HammerAndSickle.Services
{
    /// <summary>
    /// Static helper for quick name generation.
    /// Usage:  var name = NameGen.MaleName(Nationality.USA);
    /// </summary>
    public static class NameGen
    {
        private static readonly Random Rng = new();

        #region Name Data ------------------------------------------------------

        private static readonly List<string> RussianFirst = new()
        {
            "Aleksandr","Aleksei","Anatoli","Andrei","Anton","Boris","Dmitri","Fyodor","Gennadi","Georgi",
            "Igor","Ivan","Konstantin","Leonid","Maxim","Mikhail","Nikolai","Oleg","Pavel","Pyotr",
            "Roman","Sergei","Stanislav","Valeri","Vasili","Viktor","Vladimir","Vladislav","Yegor","Yuri"
        };

        private static readonly List<string> RussianLast = new()
        {
            "Ivanov","Smirnov","Kuznetsov","Popov","Vasiliev","Petrov","Sokolov","Mikhailov","Novikov","Fedorov",
            "Morozov","Volkov","Alekseev","Lebedev","Semenov","Egorov","Pavlov","Kozlov","Stepanov","Nikolaev",
            "Orlov","Andreev","Makarov","Nikitin","Zakharov","Zaytsev","Solovyov","Borisov","Yakovlev","Grigoriev",
            "Romanov","Vorobiev","Sidorov","Kuzmin","Fomin","Baranov","Filippov","Davidov","Belyaev","Tarasov",
            "Golubev","Bogdanov","Medvedev","Belov","Antonov","Gusev","Rodionov","Komarov","Polyakov","Konovalov"
        };

        private static readonly List<string> USFirst = new()
        {
            "James","John","Robert","Michael","William","David","Richard","Joseph","Thomas","Charles",
            "Christopher","Daniel","Matthew","Anthony","Mark","Donald","Steven","Paul","Andrew","Joshua",
            "Kenneth","Kevin","Brian","George","Timothy","Ronald","Edward","Jason","Jeffrey","Ryan"
        };

        private static readonly List<string> USLast = new()
        {
            "Smith","Johnson","Williams","Jones","Brown","Davis","Miller","Wilson","Moore","Taylor",
            "Anderson","Thomas","Jackson","White","Harris","Martin","Thompson","Garcia","Martinez","Robinson",
            "Clark","Rodriguez","Lewis","Lee","Walker","Hall","Allen","Young","Hernandez","King",
            "Wright","Lopez","Hill","Scott","Green","Adams","Baker","Gonzalez","Nelson","Carter",
            "Mitchell","Perez","Roberts","Turner","Phillips","Campbell","Parker","Evans","Edwards","Collins"
        };

        private static readonly List<string> UKFirst = new()
        {
            "Oliver","George","Harry","Jack","Charlie","Leo","Jacob","Freddie","Alfie","Oscar",
            "Arthur","Henry","William","Thomas","James","Theo","Noah","Edward","Ethan","Lucas",
            "Alexander","Benjamin","Mason","Harrison","Logan","Daniel","Isaac","Joseph","Samuel","Sebastian"
        };

        private static readonly List<string> UKLast = new()
        {
            "Smith","Jones","Williams","Taylor","Brown","Davies","Evans","Wilson","Thomas","Roberts",
            "Johnson","Lewis","Walker","Robinson","Wood","Thompson","White","Watson","Jackson","Wright",
            "Green","Harris","Cooper","King","Lee","Martin","Clarke","James","Morgan","Hughes",
            "Edwards","Hill","Moore","Clark","Harrison","Scott","Young","Morris","Hall","Ward",
            "Turner","Carter","Phillips","Mitchell","Patel","Adams","Campbell","Anderson","Allen","Cook"
        };

        private static readonly List<string> GermanFirst = new()
        {
            "Alexander","Maximilian","Paul","Leon","Felix","Lukas","Elias","Jonas","David","Julian",
            "Moritz","Philipp","Tim","Niklas","Jakob","Fabian","Jan","Ben","Jonathan","Simon",
            "Florian","Luca","Finn","Daniel","Sebastian","Marcel","Christian","Thomas","Andreas","Stefan"
        };

        private static readonly List<string> GermanLast = new()
        {
            "Müller","Schmidt","Schneider","Fischer","Weber","Meyer","Wagner","Becker","Schulz","Hoffmann",
            "Schäfer","Koch","Bauer","Richter","Klein","Wolf","Schröder","Neumann","Schwarz","Zimmermann",
            "Braun","Krüger","Hofmann","Hartmann","Lange","Schmitt","Werner","Schmitz","Krause","Meier",
            "Lehmann","Schmid","Schulze","Maier","Köhler","Herrmann","König","Walter","Mayer","Huber",
            "Kaiser","Fuchs","Peters","Lang","Scholz","Möller","Weiß","Jung","Hahn","Schubert"
        };

        private static readonly List<string> FrenchFirst = new()
        {
            "Jean","Pierre","Michel","André","Philippe","Louis","Nicolas","François","Henri","Bernard",
            "Jacques","Paul","Marcel","Robert","Claude","Daniel","Christian","Thomas","Joseph","Alain",
            "Antoine","Maurice","Christophe","Vincent","Guillaume","Alexandre","Julien","Sébastien","Patrick","David"
        };

        private static readonly List<string> FrenchLast = new()
        {
            "Martin","Bernard","Thomas","Petit","Robert","Richard","Durand","Dubois","Moreau","Laurent",
            "Simon","Michel","Lefebvre","Leroy","Roux","David","Bertrand","Morel","Fournier","Girard",
            "Bonnet","Dupont","Lambert","Fontaine","Rousseau","Vincent","Muller","Lefevre","Faure","Andre",
            "Mercier","Blanc","Guerin","Boyer","Garnier","Chevalier","Francois","Legrand","Gauthier","Garcia",
            "Perrin","Robin","Clement","Morin","Nicolas","Henry","Roussel","Mathieu","Gautier","Masson"
        };

        private static readonly List<string> ArabicFirst = new()
        {
            "Mohammed","Ahmed","Ali","Omar","Abdullah","Yusuf","Ibrahim","Hassan","Husain","Khalid",
            "Mahmoud","Mustafa","Abdul","Samir","Said","Tariq","Waleed","Bilal","Faisal","Majid",
            "Adil","Karim","Jamal","Hamza","Malik","Nasser","Rashid","Salim","Ziad","Fahad"
        };

        private static readonly List<string> ArabicLast = new()
        {
            "Al-Saud","Al-Ghamdi","Al-Qahtan","Al-Shammari","Al-Otaibi","Al-Qahtani","Al-Dossari","Al-Mutairi","Al-Harbi","Al-Subaie",
            "Al-Zahrani","Al-Anazi","Al-Shehri","Al-Maliki","Al-Juhani","Al-Faraj","Al-Yami","Al-Hamad","Al-Salem","Al-Hajri",
            "Al-Balawi","Al-Saleh","Al-Rashidi","Al-Ahmed","Al-Asiri","Al-Taweel","Al-Qurashi","Al-Amri","Al-Dawsari","Al-Khalidi",
            "Al-Saeed","Al-Asmari","Al-Sharif","Al-Yahya","Al-Harithi","Al-Jaber","Al-Mahdi","Al-Najjar","Al-Omari","Al-Tamimi",
            "Al-Ruwaili","Al-Harthy","Al-Zaidi","Al-Harthi","Al-Dosari","Al-Hassan","Al-Bishi","Al-Qurayshi","Al-Farsi","Al-Ammar"
        };

        #endregion -------------------------------------------------------------

        private static List<string> FirstList(Models.Nationality n) => n switch
        {
            Models.Nationality.USSR => RussianFirst,
            Models.Nationality.USA => USFirst,
            Models.Nationality.UK => UKFirst,
            Models.Nationality.FRG => GermanFirst,
            Models.Nationality.FRA => FrenchFirst,
            Models.Nationality.IR or
            Models.Nationality.IQ or
            Models.Nationality.SAUD => ArabicFirst,
            Models.Nationality.MJ => RussianFirst,
            _ => RussianFirst
        };

        private static List<string> LastList(Models.Nationality n) => n switch
        {
            Models.Nationality.USSR => RussianLast,
            Models.Nationality.USA => USLast,
            Models.Nationality.UK => UKLast,
            Models.Nationality.FRG => GermanLast,
            Models.Nationality.FRA => FrenchLast,
            Models.Nationality.IR or
            Models.Nationality.IQ or
            Models.Nationality.SAUD => ArabicLast,
            Models.Nationality.MJ => RussianLast,
            _ => RussianLast
        };

        public static string MaleName(Models.Nationality n) => $"{MaleFirstName(n)} {LastName(n)}";
        public static string MaleFirstName(Models.Nationality n) => Pick(FirstList(n));
        public static string LastName(Models.Nationality n) => Pick(LastList(n));

        private static string Pick(List<string> list) => list[Rng.Next(list.Count)];
    }
}