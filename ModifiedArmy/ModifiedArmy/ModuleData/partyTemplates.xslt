<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output omit-xml-declaration="yes"/>
<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>
    
    <xsl:template match="MBPartyTemplate[@id='main_hero_party_template']"/>

    <!-- Replace only the six ordinary land-bandit templates. -->
    <xsl:template match="MBPartyTemplate[@id='looters_template']"/>
    <xsl:template match="MBPartyTemplate[@id='sea_raiders_template']"/>
    <xsl:template match="MBPartyTemplate[@id='mountain_bandits_template']"/>
    <xsl:template match="MBPartyTemplate[@id='forest_bandits_template']"/>
    <xsl:template match="MBPartyTemplate[@id='desert_bandits_template']"/>
    <xsl:template match="MBPartyTemplate[@id='steppe_bandits_template']"/>

    <xsl:template match="MBPartyTemplate[@id='villager_empire_template']"/>
    <xsl:template match="MBPartyTemplate[@id='villager_aserai_template']"/>
    <xsl:template match="MBPartyTemplate[@id='villager_sturgia_template']"/>
    <xsl:template match="MBPartyTemplate[@id='villager_vlandia_template']"/>
    <xsl:template match="MBPartyTemplate[@id='villager_battania_template']"/>
    <xsl:template match="MBPartyTemplate[@id='villager_khuzait_template']"/>
    <xsl:template match="MBPartyTemplate[@id='villager_nord_template']"/>

    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_empire_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_sturgia_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_aserai_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_vlandia_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_battania_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_khuzait_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_nord_template']"/>

    <!-- Remove native minor-faction templates before loading the expanded definitions. -->
    <!-- Keep this list synchronized with the minor-faction block in partyTemplates.xml. -->
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_mercenary_aserai_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_mercenary_empire_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_mercenary_sturgia_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_mercenary_vlandia_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_outlaw_aserai_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_outlaw_battania_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_outlaw_empire_e_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_outlaw_empire_s_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_outlaw_empire_w_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_outlaw_sturgia_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_outlaw_vlandia_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_jawwal_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_karakhuzaits_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_forest_people_template']"/>
    <xsl:template match="MBPartyTemplate[@id='kingdom_hero_party_eleftheroi_template']"/>

</xsl:stylesheet>
