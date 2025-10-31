<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output omit-xml-declaration="yes"/>
<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>
 
    <xsl:template match="NPCCharacter[@id='khuzait_nomad']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_footman']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_tribal_warrior']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_hunter']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_spearman']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_raider']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_horseman']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_lancer']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_heavy_lancer']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_torguud']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_kheshig']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_khans_guard']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_archer']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_marksman']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_spear_infantry']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_darkhan']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_horse_archer']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_heavy_horse_archer']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_noble_son']"/>
    <xsl:template match="NPCCharacter[@id='khuzait_qanqli']"/>

</xsl:stylesheet>
